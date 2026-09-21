using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WorkTimeTracker.Models;

namespace WorkTimeTracker.Services
{
    public enum SyncMode
    {
        Local,
        GoogleDrive,
        HomeServer
    }

    public class SyncConfigData
    {
        public SyncMode Mode { get; set; } = SyncMode.Local;
        public string SyncFolder { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public class TimeLogService
    {
        public static readonly string DefaultStorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkTimeTracker");

        private static readonly string ConfigFilePath = Path.Combine(DefaultStorageDirectory, "sync_config.json");

        private string _activeDirectory = DefaultStorageDirectory;
        private string _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
        private FileSystemWatcher? _fileWatcher;
        private DateTime _lastFileWriteTime = DateTime.MinValue;
        private readonly object _lockObj = new();
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
        private System.Threading.Timer? _serverPollTimer;

        public event Action? LogsUpdatedExternally;

        public SyncMode CurrentSyncMode { get; private set; } = SyncMode.Local;
        public string ServerUrl { get; private set; } = string.Empty;
        public string ApiKey { get; private set; } = string.Empty;

        public string CurrentStorageDirectory => _activeDirectory;
        public string CurrentStorageFilePath => _activeFilePath;
        public bool IsCloudSyncActive => CurrentSyncMode != SyncMode.Local;
        public bool IsHomeServerActive => CurrentSyncMode == SyncMode.HomeServer;

        public static readonly List<string> DefaultCategories = new()
        {
            "Feature Development & Coding",
            "Production Incident & Triage",
            "Prod Support & Monitoring",
            "Bug Fixing & Investigation",
            "Code Review & Pull Requests",
            "Deployments & Releases",
            "Architecture & Tech Design",
            "Standups, Meetings & Syncs",
            "User Inquiries & On-Call Admin",
            "Documentation & Knowledge Base",
            "Break / Other"
        };

        public static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Feature Development & Coding", "#0078D4" }, // Blue
            { "Production Incident & Triage", "#E81123" }, // Crimson Red
            { "Prod Support & Monitoring", "#FF8C00" },    // Warning Orange
            { "Bug Fixing & Investigation", "#EA4335" },   // Bug Red
            { "Code Review & Pull Requests", "#8764B8" },  // Fluent Purple
            { "Deployments & Releases", "#107C41" },       // Emerald Green
            { "Architecture & Tech Design", "#00B7C3" },   // Cyan
            { "Standups, Meetings & Syncs", "#5C2D91" },   // Deep Violet
            { "User Inquiries & On-Call Admin", "#D83B01" },// Russet Orange
            { "Documentation & Knowledge Base", "#4F6BED" },// Soft Blue
            { "Break / Other", "#8A8886" }                 // Neutral Slate
        };

        private readonly List<TimeLog> _logs = new();

        public TimeLogService()
        {
            InitializeStorageLocation();
            LoadLogs();
            SetupFileWatcher();
        }

        public IReadOnlyList<TimeLog> GetAllLogs() => _logs.OrderByDescending(l => l.Timestamp).ToList();

        public void AddLog(TimeLog log)
        {
            _logs.Add(log);
            SaveLogs();
            if (CurrentSyncMode == SyncMode.HomeServer)
            {
                _ = SyncWithServerAsync();
            }
        }

        public void UpdateLog(TimeLog updatedLog)
        {
            var existing = _logs.FirstOrDefault(l => l.Id == updatedLog.Id);
            if (existing != null)
            {
                existing.Timestamp = updatedLog.Timestamp;
                existing.Duration = updatedLog.Duration;
                existing.Category = updatedLog.Category;
                existing.Description = updatedLog.Description;
                SaveLogs();
                if (CurrentSyncMode == SyncMode.HomeServer)
                {
                    _ = SyncWithServerAsync();
                }
            }
        }

        public void DeleteLog(Guid id)
        {
            var existing = _logs.FirstOrDefault(l => l.Id == id);
            if (existing != null)
            {
                _logs.Remove(existing);
                SaveLogs();
                if (CurrentSyncMode == SyncMode.HomeServer)
                {
                    _ = DeleteLogFromServerAsync(id);
                }
            }
        }

        public List<TimeLog> GetLogsForDate(DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);
            return _logs
                .Where(l => l.Timestamp >= start && l.Timestamp < end)
                .OrderBy(l => l.Timestamp)
                .ToList();
        }

        public TimeSpan GetTotalDurationForDate(DateTime date)
        {
            var logs = GetLogsForDate(date);
            return TimeSpan.FromMinutes(logs.Sum(l => l.Duration.TotalMinutes));
        }

        public (DateTime Start, DateTime End) GetCurrentWorkWeekRange()
        {
            return GetWeekRangeForDate(DateTime.Today);
        }

        public (DateTime Start, DateTime End) GetWeekRangeForDate(DateTime date)
        {
            var day = date.Date;
            int daysSinceMonday = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime mondayStart = day.AddDays(-daysSinceMonday);
            DateTime sundayEnd = mondayStart.AddDays(7).AddTicks(-1);
            return (mondayStart, sundayEnd);
        }

        public List<TimeLog> GetLogsForCurrentWorkWeek()
        {
            var (start, _) = GetCurrentWorkWeekRange();
            return GetLogsForWeek(start);
        }

        public List<TimeLog> GetLogsForWeek(DateTime weekStart)
        {
            var start = weekStart.Date;
            var end = start.AddDays(7);
            return _logs
                .Where(l => l.Timestamp >= start && l.Timestamp < end)
                .OrderByDescending(l => l.Timestamp)
                .ToList();
        }

        public List<CategorySummary> GetCategorySummariesForCurrentWeek()
        {
            var (start, _) = GetCurrentWorkWeekRange();
            return GetCategorySummariesForWeek(start);
        }

        public List<CategorySummary> GetCategorySummariesForWeek(DateTime weekStart)
        {
            var weekLogs = GetLogsForWeek(weekStart);
            if (weekLogs.Count == 0)
                return new List<CategorySummary>();

            double totalMinutes = weekLogs.Sum(l => l.Duration.TotalMinutes);

            return weekLogs
                .GroupBy(l => string.IsNullOrWhiteSpace(l.Category) ? "Uncategorized" : l.Category.Trim())
                .Select(g =>
                {
                    double catMinutes = g.Sum(l => l.Duration.TotalMinutes);
                    string catName = g.Key;
                    string color = CategoryColors.TryGetValue(catName, out var hex) ? hex : "#5C2D91";

                    return new CategorySummary
                    {
                        Category = catName,
                        TotalDuration = TimeSpan.FromMinutes(catMinutes),
                        Count = g.Count(),
                        Percentage = totalMinutes > 0 ? (catMinutes / totalMinutes) * 100.0 : 0,
                        ColorHex = color
                    };
                })
                .OrderByDescending(s => s.TotalDuration)
                .ToList();
        }

        public TimeSpan GetTotalDurationForCurrentWeek()
        {
            var (start, _) = GetCurrentWorkWeekRange();
            return GetTotalDurationForWeek(start);
        }

        public TimeSpan GetTotalDurationForWeek(DateTime weekStart)
        {
            var weekLogs = GetLogsForWeek(weekStart);
            return TimeSpan.FromMinutes(weekLogs.Sum(l => l.Duration.TotalMinutes));
        }

        public TimeSpan GetTotalDurationToday()
        {
            return GetTotalDurationForDate(DateTime.Today);
        }

        public List<DaySummary> GetDaysSummaryForMonth(int year, int month, DateTime? selectedDate = null)
        {
            var firstOfMonth = new DateTime(year, month, 1);
            int daysSinceMonday = ((int)firstOfMonth.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var startDate = firstOfMonth.AddDays(-daysSinceMonday);

            var days = new List<DaySummary>();
            // Generate 35 or 42 calendar cells
            for (int i = 0; i < 42; i++)
            {
                var currentDate = startDate.AddDays(i);
                
                // If we've finished the month and ended on a Sunday, break after row 5 (35)
                if (i >= 35 && currentDate.Month != month && currentDate.DayOfWeek == DayOfWeek.Monday)
                {
                    break;
                }

                var duration = GetTotalDurationForDate(currentDate);
                var logsCount = GetLogsForDate(currentDate).Count;

                days.Add(new DaySummary
                {
                    Date = currentDate,
                    TotalDuration = duration,
                    Count = logsCount,
                    IsCurrentMonth = currentDate.Month == month,
                    IsSelected = selectedDate.HasValue && selectedDate.Value.Date == currentDate.Date
                });
            }

            return days;
        }

        private void InitializeStorageLocation()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var config = JsonSerializer.Deserialize<SyncConfigData>(json, options);
                    if (config != null)
                    {
                        if (config.Mode == SyncMode.HomeServer && !string.IsNullOrWhiteSpace(config.ServerUrl))
                        {
                            CurrentSyncMode = SyncMode.HomeServer;
                            ServerUrl = config.ServerUrl;
                            ApiKey = config.ApiKey;
                            _activeDirectory = DefaultStorageDirectory;
                            _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
                            StartServerPolling();
                            _ = SyncWithServerAsync();
                            return;
                        }
                        else if (!string.IsNullOrWhiteSpace(config.SyncFolder) && Directory.Exists(config.SyncFolder))
                        {
                            CurrentSyncMode = SyncMode.GoogleDrive;
                            _activeDirectory = config.SyncFolder;
                            _activeFilePath = Path.Combine(_activeDirectory, "timelogs.json");
                            return;
                        }
                    }
                }
            }
            catch { }

            // Auto-detect Google Drive if available
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] googleDriveCandidates = new[]
            {
                @"G:\My Drive\WorkTimeTracker",
                @"G:\My Drive",
                Path.Combine(userProfile, "Google Drive", "WorkTimeTracker"),
                Path.Combine(userProfile, "Google Drive"),
                Path.Combine(userProfile, "My Drive")
            };

            foreach (var candidate in googleDriveCandidates)
            {
                if (Directory.Exists(candidate))
                {
                    string target = candidate.EndsWith("WorkTimeTracker", StringComparison.OrdinalIgnoreCase) 
                        ? candidate 
                        : Path.Combine(candidate, "WorkTimeTracker");
                    
                    try
                    {
                        Directory.CreateDirectory(target);
                        CurrentSyncMode = SyncMode.GoogleDrive;
                        _activeDirectory = target;
                        _activeFilePath = Path.Combine(_activeDirectory, "timelogs.json");
                        SaveSyncConfig();
                        return;
                    }
                    catch { }
                }
            }

            CurrentSyncMode = SyncMode.Local;
            _activeDirectory = DefaultStorageDirectory;
            _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
        }

        public async Task ConfigureHomeServerAsync(string url, string apiKey)
        {
            StopServerPolling();
            CurrentSyncMode = SyncMode.HomeServer;
            ServerUrl = url.Trim();
            ApiKey = apiKey.Trim();
            _activeDirectory = DefaultStorageDirectory;
            _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");

            SaveSyncConfig();
            SetupFileWatcher();
            StartServerPolling();
            await SyncWithServerAsync();
            LogsUpdatedExternally?.Invoke();
        }

        public void ConfigureGoogleDrive(string newDirectory)
        {
            StopServerPolling();
            if (string.IsNullOrWhiteSpace(newDirectory))
            {
                ResetToLocal();
                return;
            }

            try
            {
                Directory.CreateDirectory(newDirectory);
                CurrentSyncMode = SyncMode.GoogleDrive;
                _activeDirectory = newDirectory;
                _activeFilePath = Path.Combine(_activeDirectory, "timelogs.json");

                // If timelogs.json doesn't exist in new folder, copy existing logs there
                if (!File.Exists(_activeFilePath) && _logs.Count > 0)
                {
                    SaveLogs();
                }
                else if (File.Exists(_activeFilePath))
                {
                    LoadLogs();
                }

                SaveSyncConfig();
                SetupFileWatcher();
                LogsUpdatedExternally?.Invoke();
            }
            catch { }
        }

        public void SetCustomStorageDirectory(string newDirectory)
        {
            ConfigureGoogleDrive(newDirectory);
        }

        public void ResetToLocal()
        {
            StopServerPolling();
            CurrentSyncMode = SyncMode.Local;
            ServerUrl = string.Empty;
            ApiKey = string.Empty;
            _activeDirectory = DefaultStorageDirectory;
            _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
            SaveSyncConfig();
            SetupFileWatcher();
            LoadLogs();
            LogsUpdatedExternally?.Invoke();
        }

        public void ResetToDefaultLocalDirectory()
        {
            ResetToLocal();
        }

        public async Task<(bool Success, string Message)> TestServerConnectionAsync(string url, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (false, "Server URL cannot be empty.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return (false, "Invalid URL format. Please include http:// or https:// (e.g. http://myhome.ddns.net:48291).");

            try
            {
                var healthUrl = url.TrimEnd('/') + "/api/health";
                var healthResponse = await _httpClient.GetAsync(healthUrl);
                if (!healthResponse.IsSuccessStatusCode)
                {
                    return (false, $"Health check failed with status: {healthResponse.StatusCode}");
                }

                var logsUrl = url.TrimEnd('/') + "/api/logs";
                using var logsRequest = new HttpRequestMessage(HttpMethod.Get, logsUrl);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    logsRequest.Headers.Add("X-API-Key", apiKey.Trim());
                }

                var logsResponse = await _httpClient.SendAsync(logsRequest);
                if (logsResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "Connected to server, but the API Key was rejected (401 Unauthorized). Please check your API_KEY.");
                }

                if (!logsResponse.IsSuccessStatusCode)
                {
                    return (false, $"Server returned error: {logsResponse.StatusCode}");
                }

                return (true, "Successfully connected to WorkTimeTracker Docker Server!");
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        public async Task SyncWithServerAsync()
        {
            if (CurrentSyncMode != SyncMode.HomeServer || string.IsNullOrWhiteSpace(ServerUrl))
                return;

            try
            {
                var requestUrl = ServerUrl.TrimEnd('/') + "/api/logs/sync";
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                if (!string.IsNullOrWhiteSpace(ApiKey))
                {
                    request.Headers.Add("X-API-Key", ApiKey.Trim());
                }

                List<TimeLog> currentLogsCopy;
                lock (_lockObj)
                {
                    currentLogsCopy = _logs.ToList();
                }

                var jsonPayload = JsonSerializer.Serialize(currentLogsCopy);
                request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var serverLogs = JsonSerializer.Deserialize<List<TimeLog>>(responseJson, options);
                    if (serverLogs != null)
                    {
                        bool hasDifferences = false;
                        lock (_lockObj)
                        {
                            if (serverLogs.Count != _logs.Count || !serverLogs.Select(s => s.Id).SequenceEqual(_logs.Select(l => l.Id)))
                            {
                                hasDifferences = true;
                                _logs.Clear();
                                _logs.AddRange(serverLogs);
                            }
                        }

                        if (hasDifferences)
                        {
                            SaveLogs();
                            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                LogsUpdatedExternally?.Invoke();
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private async Task DeleteLogFromServerAsync(Guid id)
        {
            if (CurrentSyncMode != SyncMode.HomeServer || string.IsNullOrWhiteSpace(ServerUrl))
                return;

            try
            {
                var url = ServerUrl.TrimEnd('/') + $"/api/logs/{id}";
                using var req = new HttpRequestMessage(HttpMethod.Delete, url);
                if (!string.IsNullOrWhiteSpace(ApiKey))
                {
                    req.Headers.Add("X-API-Key", ApiKey.Trim());
                }
                await _httpClient.SendAsync(req);
            }
            catch { }
        }

        private void StartServerPolling()
        {
            StopServerPolling();
            _serverPollTimer = new System.Threading.Timer(async _ =>
            {
                await SyncWithServerAsync();
            }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        }

        private void StopServerPolling()
        {
            _serverPollTimer?.Dispose();
            _serverPollTimer = null;
        }

        private void SaveSyncConfig()
        {
            try
            {
                Directory.CreateDirectory(DefaultStorageDirectory);
                var config = new SyncConfigData
                {
                    Mode = CurrentSyncMode,
                    SyncFolder = CurrentSyncMode == SyncMode.GoogleDrive ? _activeDirectory : string.Empty,
                    ServerUrl = ServerUrl,
                    ApiKey = ApiKey
                };
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }

        private void SetupFileWatcher()
        {
            try
            {
                _fileWatcher?.Dispose();
                _fileWatcher = null;

                if (!Directory.Exists(_activeDirectory))
                {
                    Directory.CreateDirectory(_activeDirectory);
                }

                _fileWatcher = new FileSystemWatcher(_activeDirectory)
                {
                    Filter = Path.GetFileName(_activeFilePath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged;
            }
            catch { }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lockObj)
            {
                // Debounce rapid writes
                if ((DateTime.Now - _lastFileWriteTime).TotalMilliseconds < 800)
                {
                    return;
                }
                _lastFileWriteTime = DateTime.Now;
            }

            // Brief pause to allow file stream close
            System.Threading.Thread.Sleep(200);

            LoadLogs();
            LogsUpdatedExternally?.Invoke();
        }

        private void LoadLogs()
        {
            try
            {
                string targetPath = _activeFilePath;
                // If timelogs.json does not exist yet, check legacy time_logs.json in local app data
                if (!File.Exists(targetPath))
                {
                    string legacyPath = Path.Combine(DefaultStorageDirectory, "time_logs.json");
                    if (File.Exists(legacyPath))
                    {
                        targetPath = legacyPath;
                    }
                }

                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath);
                    var loaded = JsonSerializer.Deserialize<List<TimeLog>>(json);
                    if (loaded != null)
                    {
                        _logs.Clear();
                        _logs.AddRange(loaded);
                    }
                }
            }
            catch
            {
                // In case of any deserialization failure, start fresh rather than crashing
            }
        }

        private void SaveLogs()
        {
            try
            {
                if (!Directory.Exists(_activeDirectory))
                {
                    Directory.CreateDirectory(_activeDirectory);
                }

                lock (_lockObj)
                {
                    _lastFileWriteTime = DateTime.Now;
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_logs, options);
                File.WriteAllText(_activeFilePath, json);

                // Also keep a local backup in local appdata if custom directory is active
                if (IsCloudSyncActive)
                {
                    string backupPath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
                    File.WriteAllText(backupPath, json);
                }
            }
            catch
            {
                // Silent fail or log
            }
        }
    }
}
