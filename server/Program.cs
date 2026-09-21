using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to handle PascalCase and camelCase smoothly
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.WriteIndented = true;
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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

    // Check ?key= query parameter (for direct browser downloads & web app links)
    if (context.Request.Query.TryGetValue("key", out var queryKey) &&
        string.Equals(queryKey.ToString().Trim(), configuredApiKey.Trim(), StringComparison.Ordinal))
    {
        return true;
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

// 7. GET Excel Export (/api/export/excel)
app.MapGet("/api/export/excel", (HttpContext ctx) =>
{
    if (!IsAuthorized(ctx)) return Results.Unauthorized();

    DateTime weekStart;
    if (ctx.Request.Query.TryGetValue("weekStart", out var wsStr) && DateTime.TryParse(wsStr, out var parsedWs))
    {
        weekStart = parsedWs.Date;
    }
    else if (ctx.Request.Query.TryGetValue("date", out var dStr) && DateTime.TryParse(dStr, out var parsedDate))
    {
        weekStart = parsedDate.Date;
    }
    else
    {
        weekStart = DateTime.Today;
    }

    // Calculate Monday - Sunday week range
    int daysSinceMonday = ((int)weekStart.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
    DateTime monday = weekStart.AddDays(-daysSinceMonday).Date;
    DateTime sunday = monday.AddDays(7).AddTicks(-1);

    var allLogs = LoadLogs();
    var weekLogs = allLogs
        .Where(l => l.Timestamp >= monday && l.Timestamp <= sunday)
        .OrderBy(l => l.Timestamp)
        .ToList();

    using var workbook = new XLWorkbook();

    // -------------------------------------------------------------
    // SHEET 1: WEEKLY SUMMARY
    // -------------------------------------------------------------
    var wsSummary = workbook.Worksheets.Add("Weekly Summary");
    wsSummary.ShowGridLines = true;

    // Title Block
    wsSummary.Cell("A1").Value = "WORK TIME TRACKER — WEEKLY REPORT";
    wsSummary.Cell("A1").Style.Font.Bold = true;
    wsSummary.Cell("A1").Style.Font.FontSize = 16;
    wsSummary.Cell("A1").Style.Font.FontColor = XLColor.FromHtml("#1F4E79");

    wsSummary.Cell("A2").Value = $"Work Week: {monday:ddd, MMM d, yyyy} – {sunday:ddd, MMM d, yyyy}";
    wsSummary.Cell("A2").Style.Font.Italic = true;
    wsSummary.Cell("A2").Style.Font.FontSize = 11;
    wsSummary.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#555555");

    wsSummary.Cell("A3").Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm} (Web Home Server)";
    wsSummary.Cell("A3").Style.Font.FontSize = 9;
    wsSummary.Cell("A3").Style.Font.FontColor = XLColor.FromHtml("#888888");

    // KPI Summary
    double totalWeekHours = 0;
    foreach (var l in weekLogs)
    {
        if (TimeSpan.TryParse(l.Duration, out var ts))
        {
            totalWeekHours += ts.TotalHours;
        }
    }

    int kpiRow = 5;
    wsSummary.Cell(kpiRow, 1).Value = "Total Tracked Hours:";
    wsSummary.Cell(kpiRow, 1).Style.Font.Bold = true;
    wsSummary.Cell(kpiRow, 2).Value = Math.Round(totalWeekHours, 2);
    wsSummary.Cell(kpiRow, 2).Style.NumberFormat.Format = "0.0 \"hrs\"";
    wsSummary.Cell(kpiRow, 2).Style.Font.Bold = true;

    wsSummary.Cell(kpiRow + 1, 1).Value = "Total 30m Blocks:";
    wsSummary.Cell(kpiRow + 1, 1).Style.Font.Bold = true;
    wsSummary.Cell(kpiRow + 1, 2).Value = weekLogs.Count;

    // Section 1: Categories Breakdown
    int catStartRow = 8;
    wsSummary.Cell(catStartRow, 1).Value = "CATEGORY BREAKDOWN";
    wsSummary.Cell(catStartRow, 1).Style.Font.Bold = true;
    wsSummary.Cell(catStartRow, 1).Style.Font.FontSize = 12;
    wsSummary.Cell(catStartRow, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E79");

    int catHeaderRow = catStartRow + 1;
    string[] catHeaders = { "Category", "Hours Spent", "30-Min Blocks", "% of Total Week" };
    for (int col = 0; col < catHeaders.Length; col++)
    {
        var cell = wsSummary.Cell(catHeaderRow, col + 1);
        cell.Value = catHeaders[col];
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
        cell.Style.Alignment.Horizontal = col == 0 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
    }

    var catGroups = weekLogs
        .GroupBy(l => string.IsNullOrWhiteSpace(l.Category) ? "Uncategorized" : l.Category.Trim())
        .Select(g =>
        {
            double hours = 0;
            foreach (var l in g)
            {
                if (TimeSpan.TryParse(l.Duration, out var ts)) hours += ts.TotalHours;
            }
            return new { Category = g.Key, Hours = hours, Count = g.Count() };
        })
        .OrderByDescending(c => c.Hours)
        .ToList();

    int currRow = catHeaderRow + 1;
    foreach (var cat in catGroups)
    {
        wsSummary.Cell(currRow, 1).Value = cat.Category;

        var hCell = wsSummary.Cell(currRow, 2);
        hCell.Value = Math.Round(cat.Hours, 2);
        hCell.Style.NumberFormat.Format = "0.00";
        hCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var cCell = wsSummary.Cell(currRow, 3);
        cCell.Value = cat.Count;
        cCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var pctCell = wsSummary.Cell(currRow, 4);
        pctCell.Value = totalWeekHours > 0 ? (cat.Hours / totalWeekHours) : 0.0;
        pctCell.Style.NumberFormat.Format = "0.0%";
        pctCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        if ((currRow - catHeaderRow) % 2 == 0)
        {
            wsSummary.Range(currRow, 1, currRow, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F5F8");
        }

        currRow++;
    }

    if (catGroups.Count > 0)
    {
        wsSummary.Cell(currRow, 1).Value = "Grand Total";
        wsSummary.Cell(currRow, 1).Style.Font.Bold = true;

        var totalHoursCell = wsSummary.Cell(currRow, 2);
        totalHoursCell.FormulaA1 = $"=SUM(B{catHeaderRow + 1}:B{currRow - 1})";
        totalHoursCell.Style.Font.Bold = true;
        totalHoursCell.Style.NumberFormat.Format = "0.00";

        var totalCountCell = wsSummary.Cell(currRow, 3);
        totalCountCell.FormulaA1 = $"=SUM(C{catHeaderRow + 1}:C{currRow - 1})";
        totalCountCell.Style.Font.Bold = true;

        var totalPctCell = wsSummary.Cell(currRow, 4);
        totalPctCell.Value = 1.0;
        totalPctCell.Style.Font.Bold = true;
        totalPctCell.Style.NumberFormat.Format = "0.0%";

        wsSummary.Range(currRow, 1, currRow, 4).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        wsSummary.Range(currRow, 1, currRow, 4).Style.Border.BottomBorder = XLBorderStyleValues.Double;
    }

    // Section 2: Daily Hours & 7.5h Target
    int dayStartRow = currRow + 3;
    wsSummary.Cell(dayStartRow, 1).Value = "DAILY WORK LOG & 7.5-HOUR TARGET";
    wsSummary.Cell(dayStartRow, 1).Style.Font.Bold = true;
    wsSummary.Cell(dayStartRow, 1).Style.Font.FontSize = 12;
    wsSummary.Cell(dayStartRow, 1).Style.Font.FontColor = XLColor.FromHtml("#1F4E79");

    int dayHeaderRow = dayStartRow + 1;
    string[] dayHeaders = { "Date", "Day", "Hours Logged", "Target (hrs)", "Target Met (>= 7.5h)" };
    for (int col = 0; col < dayHeaders.Length; col++)
    {
        var cell = wsSummary.Cell(dayHeaderRow, col + 1);
        cell.Value = dayHeaders[col];
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5597");
        cell.Style.Alignment.Horizontal = col < 2 ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Right;
    }

    int dayCurrRow = dayHeaderRow + 1;
    int targetMetCount = 0;
    for (int d = 0; d < 7; d++)
    {
        var currentDay = monday.AddDays(d);
        var dayLogs = weekLogs.Where(l => l.Timestamp.Date == currentDay.Date).ToList();
        double dayHours = 0;
        foreach (var l in dayLogs)
        {
            if (TimeSpan.TryParse(l.Duration, out var ts)) dayHours += ts.TotalHours;
        }

        bool isTargetMet = dayHours >= 7.5;
        if (isTargetMet) targetMetCount++;

        wsSummary.Cell(dayCurrRow, 1).Value = currentDay.ToString("yyyy-MM-dd");
        wsSummary.Cell(dayCurrRow, 2).Value = currentDay.ToString("dddd");

        var dayHoursCell = wsSummary.Cell(dayCurrRow, 3);
        dayHoursCell.Value = Math.Round(dayHours, 2);
        dayHoursCell.Style.NumberFormat.Format = "0.00";
        dayHoursCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var targetCell = wsSummary.Cell(dayCurrRow, 4);
        targetCell.Value = 7.50;
        targetCell.Style.NumberFormat.Format = "0.00";
        targetCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var statusCell = wsSummary.Cell(dayCurrRow, 5);
        if (isTargetMet)
        {
            statusCell.Value = "✓ Target Met";
            statusCell.Style.Font.FontColor = XLColor.FromHtml("#107C41");
            statusCell.Style.Font.Bold = true;
        }
        else
        {
            statusCell.Value = dayHours > 0 ? $"{dayHours:0.0}h / 7.5h" : "No hours";
            statusCell.Style.Font.FontColor = XLColor.FromHtml("#888888");
        }
        statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        if (d % 2 == 1)
        {
            wsSummary.Range(dayCurrRow, 1, dayCurrRow, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
        }

        dayCurrRow++;
    }

    wsSummary.Cell(kpiRow + 2, 1).Value = "7.5h Target Days Met:";
    wsSummary.Cell(kpiRow + 2, 1).Style.Font.Bold = true;
    wsSummary.Cell(kpiRow + 2, 2).Value = $"{targetMetCount} of 7 days";
    wsSummary.Cell(kpiRow + 2, 2).Style.Font.Bold = true;
    wsSummary.Cell(kpiRow + 2, 2).Style.Font.FontColor = targetMetCount >= 5 ? XLColor.FromHtml("#107C41") : XLColor.FromHtml("#D83B01");

    wsSummary.Columns().AdjustToContents();

    // -------------------------------------------------------------
    // SHEET 2: DETAILED ACTIVITY LOG
    // -------------------------------------------------------------
    var wsDetails = workbook.Worksheets.Add("Activity Log");
    wsDetails.ShowGridLines = true;

    string[] detailHeaders = { "Date", "Day", "Time", "Category", "Duration (Hours)", "Duration (Mins)", "Notes / Ticket Description" };
    for (int col = 0; col < detailHeaders.Length; col++)
    {
        var cell = wsDetails.Cell(1, col + 1);
        cell.Value = detailHeaders[col];
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
    }

    int logRow = 2;
    foreach (var log in weekLogs)
    {
        wsDetails.Cell(logRow, 1).Value = log.Timestamp.ToString("yyyy-MM-dd");
        wsDetails.Cell(logRow, 2).Value = log.Timestamp.ToString("ddd");
        wsDetails.Cell(logRow, 3).Value = log.Timestamp.ToString("h:mm tt");
        wsDetails.Cell(logRow, 4).Value = log.Category;

        TimeSpan duration = TimeSpan.FromMinutes(30);
        if (TimeSpan.TryParse(log.Duration, out var ts)) duration = ts;

        var hrsCell = wsDetails.Cell(logRow, 5);
        hrsCell.Value = Math.Round(duration.TotalHours, 2);
        hrsCell.Style.NumberFormat.Format = "0.00";

        var minCell = wsDetails.Cell(logRow, 6);
        minCell.Value = (int)duration.TotalMinutes;

        wsDetails.Cell(logRow, 7).Value = log.Description;

        if (logRow % 2 == 1)
        {
            wsDetails.Range(logRow, 1, logRow, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F9FA");
        }

        logRow++;
    }

    if (weekLogs.Count == 0)
    {
        wsDetails.Cell(2, 1).Value = "No log entries recorded for this work week.";
        wsDetails.Range(2, 1, 2, 7).Merge().Style.Font.Italic = true;
    }

    wsDetails.Columns().AdjustToContents();

    var memoryStream = new MemoryStream();
    workbook.SaveAs(memoryStream);
    memoryStream.Position = 0;

    var fileName = $"WorkTimeTracker_Week_{monday:yyyy-MM-dd}.xlsx";
    return Results.File(
        fileStream: memoryStream,
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: fileName
    );
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
