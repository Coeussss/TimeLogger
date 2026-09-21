using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to handle PascalCase and camelCase smoothly
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

var configuredApiKey = Environment.GetEnvironmentVariable("API_KEY") 
    ?? builder.Configuration["API_KEY"] 
    ?? "changeme_worktimetracker_key";

var dataDir = Environment.GetEnvironmentVariable("DATA_DIR") 
    ?? Path.Combine(AppContext.BaseDirectory, "data");

Directory.CreateDirectory(dataDir);
var dataFilePath = Path.Combine(dataDir, "timelogs.json");

var fileLock = new object();

// Helper to load logs safely
List<TimeLogDto> LoadLogs()
{
    lock (fileLock)
    {
        if (!File.Exists(dataFilePath)) return new List<TimeLogDto>();
        try
        {
            var json = File.ReadAllText(dataFilePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<TimeLogDto>>(json, options) ?? new List<TimeLogDto>();
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Error reading {Path}", dataFilePath);
            return new List<TimeLogDto>();
        }
    }
}

// Helper to save logs safely and reliably
void SaveLogs(List<TimeLogDto> logs)
{
    lock (fileLock)
    {
        try
        {
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(logs, options);
            File.WriteAllText(dataFilePath, json);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Error saving {Path}", dataFilePath);
            throw;
        }
    }
}

// 1. Healthcheck endpoint (Public / Unauthenticated)
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    version = "1.0.0",
    service = "WorkTimeTracker Server",
    timestamp = DateTime.UtcNow
}));

// API Key Authentication Filter for protected routes
bool IsAuthorized(HttpContext context)
{
    // Check X-API-Key header
    if (context.Request.Headers.TryGetValue("X-API-Key", out var headerKey) &&
        string.Equals(headerKey.ToString().Trim(), configuredApiKey.Trim(), StringComparison.Ordinal))
    {
        return true;
    }

    // Check Authorization: Bearer <key>
    if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var val = authHeader.ToString().Trim();
        if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = val.Substring(7).Trim();
            if (string.Equals(token, configuredApiKey.Trim(), StringComparison.Ordinal))
            {
                return true;
            }
        }
    }

    return false;
}

// 2. GET all logs
app.MapGet("/api/logs", (HttpContext ctx) =>
{
    if (!IsAuthorized(ctx)) return Results.Unauthorized();

    var logs = LoadLogs();
    return Results.Ok(logs);
});

// 3. POST single or batch logs
app.MapPost("/api/logs", async (HttpContext ctx) =>
{
    if (!IsAuthorized(ctx)) return Results.Unauthorized();

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    List<TimeLogDto> incomingLogs = new();
    try
    {
        if (body.TrimStart().StartsWith("["))
        {
            incomingLogs = JsonSerializer.Deserialize<List<TimeLogDto>>(body, options) ?? new();
        }
        else
        {
            var single = JsonSerializer.Deserialize<TimeLogDto>(body, options);
            if (single != null) incomingLogs.Add(single);
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = "Invalid JSON format", details = ex.Message });
    }

    var existing = LoadLogs();
    var dict = existing.ToDictionary(l => l.Id, l => l);

    foreach (var log in incomingLogs)
    {
        if (log.Id == Guid.Empty) log.Id = Guid.NewGuid();
        dict[log.Id] = log;
    }

    var merged = dict.Values.OrderByDescending(l => l.Timestamp).ToList();
    try
    {
        SaveLogs(merged);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to save logs to {Path}", dataFilePath);
        return Results.Problem(
            statusCode: 500,
            title: "Database write error",
            detail: $"Server failed to write to '{dataFilePath}': {ex.Message}. Check directory permissions on the host."
        );
    }

    return Results.Ok(new { message = "Saved successfully", count = merged.Count, updated = incomingLogs.Count });
});

// 4. POST two-way sync (/api/logs/sync)
app.MapPost("/api/logs/sync", async (HttpContext ctx) =>
{
    if (!IsAuthorized(ctx)) return Results.Unauthorized();

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    List<TimeLogDto> clientLogs = new();
    if (!string.IsNullOrWhiteSpace(body))
    {
        try
        {
            if (body.TrimStart().StartsWith("["))
            {
                clientLogs = JsonSerializer.Deserialize<List<TimeLogDto>>(body, options) ?? new();
            }
            else
            {
                var syncReq = JsonSerializer.Deserialize<SyncRequestDto>(body, options);
                clientLogs = syncReq?.Logs ?? new();
            }
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = "Invalid JSON format", details = ex.Message });
        }
    }

    var serverLogs = LoadLogs();
    var mergedDict = new Dictionary<Guid, TimeLogDto>();

    // Add server logs
    foreach (var l in serverLogs)
    {
        if (l.Id != Guid.Empty) mergedDict[l.Id] = l;
    }

    // Merge client logs
    foreach (var l in clientLogs)
    {
        if (l.Id == Guid.Empty) l.Id = Guid.NewGuid();
        mergedDict[l.Id] = l;
    }

    var result = mergedDict.Values.OrderByDescending(l => l.Timestamp).ToList();
    try
    {
        SaveLogs(result);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to save logs to {Path} during sync", dataFilePath);
        return Results.Problem(
            statusCode: 500,
            title: "Database write error",
            detail: $"Server failed to write to '{dataFilePath}': {ex.Message}. Check file permissions for {dataDir} on the host."
        );
    }

    return Results.Ok(result);
});

// 5. DELETE log by ID
app.MapDelete("/api/logs/{id:guid}", (Guid id, HttpContext ctx) =>
{
    if (!IsAuthorized(ctx)) return Results.Unauthorized();

    var existing = LoadLogs();
    var removed = existing.RemoveAll(l => l.Id == id);
    if (removed > 0)
    {
        try
        {
            SaveLogs(existing);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to save logs after delete to {Path}", dataFilePath);
            return Results.Problem(
                statusCode: 500,
                title: "Database write error",
                detail: $"Server failed to write to '{dataFilePath}': {ex.Message}. Check file permissions for {dataDir} on the host."
            );
        }
        return Results.Ok(new { message = "Log deleted", id });
    }

    return Results.NotFound(new { error = "Log not found", id });
});

// 6. GET Today's summary & 7.5h target
app.MapGet("/api/summary/today", (HttpContext ctx) =>
{
    if (!IsAuthorized(ctx)) return Results.Unauthorized();

    var today = DateTime.Today;
    var logs = LoadLogs()
        .Where(l => l.Timestamp.Date == today)
        .ToList();

    double totalMinutes = 0;
    foreach (var l in logs)
    {
        if (TimeSpan.TryParse(l.Duration, out var ts))
        {
            totalMinutes += ts.TotalMinutes;
        }
    }

    var totalHours = totalMinutes / 60.0;
    return Results.Ok(new
    {
        date = today.ToString("yyyy-MM-dd"),
        blockCount = logs.Count,
        totalHours = Math.Round(totalHours, 2),
        targetHours = 7.5,
        targetMet = totalHours >= 7.5,
        logs
    });
});

app.Logger.LogInformation("WorkTimeTracker Server starting on port 8080 (Data: {DataDir})", dataDir);
app.Run();

// Data Transfer Models
public class TimeLogDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Duration { get; set; } = "00:30:00";
    public string Category { get; set; } = "Feature Development & Coding";
    public string Description { get; set; } = string.Empty;
}

public class SyncRequestDto
{
    public List<TimeLogDto> Logs { get; set; } = new();
}
