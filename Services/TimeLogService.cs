using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WorkTimeTracker.Models;

namespace WorkTimeTracker.Services
{
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

        public event Action? LogsUpdatedExternally;

        public string CurrentStorageDirectory => _activeDirectory;
        public string CurrentStorageFilePath => _activeFilePath;
        public bool IsCloudSyncActive => !string.Equals(_activeDirectory, DefaultStorageDirectory, StringComparison.OrdinalIgnoreCase);

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
            }
        }

        public void DeleteLog(Guid id)
        {
            var existing = _logs.FirstOrDefault(l => l.Id == id);
            if (existing != null)
            {
                _logs.Remove(existing);
                SaveLogs();
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
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("SyncFolder", out var prop))
                    {
                        string? folder = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                        {
                            _activeDirectory = folder;
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
                        _activeDirectory = target;
                        _activeFilePath = Path.Combine(_activeDirectory, "timelogs.json");
                        SaveSyncConfig(_activeDirectory);
                        return;
                    }
                    catch { }
                }
            }

            _activeDirectory = DefaultStorageDirectory;
            _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
        }

        public void SetCustomStorageDirectory(string newDirectory)
        {
            if (string.IsNullOrWhiteSpace(newDirectory))
            {
                ResetToDefaultLocalDirectory();
                return;
            }

            try
            {
                Directory.CreateDirectory(newDirectory);
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

                SaveSyncConfig(_activeDirectory);
                SetupFileWatcher();
                LogsUpdatedExternally?.Invoke();
            }
            catch { }
        }

        public void ResetToDefaultLocalDirectory()
        {
            _activeDirectory = DefaultStorageDirectory;
            _activeFilePath = Path.Combine(DefaultStorageDirectory, "timelogs.json");
            SaveSyncConfig(string.Empty);
            SetupFileWatcher();
            LoadLogs();
            LogsUpdatedExternally?.Invoke();
        }

        private void SaveSyncConfig(string folder)
        {
            try
            {
                Directory.CreateDirectory(DefaultStorageDirectory);
                string json = JsonSerializer.Serialize(new { SyncFolder = folder }, new JsonSerializerOptions { WriteIndented = true });
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
