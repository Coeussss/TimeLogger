using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WorkTimeTracker.Models;
using WorkTimeTracker.Services;
using Wpf.Ui.Controls;

namespace WorkTimeTracker
{
    public partial class MainWindow : FluentWindow
    {
        public static IValueConverter StringToVisibilityConverter { get; } = new StringNotEmptyConverter();
        public static IValueConverter BooleanToVisibilityConverter { get; } = new System.Windows.Controls.BooleanToVisibilityConverter();
        public static IValueConverter SelectedBorderThicknessConverter { get; } = new SelectedThicknessConverter();
        public static IValueConverter SelectedBorderBrushConverter { get; } = new SelectedBrushConverter();
        public static IValueConverter CurrentMonthBackgroundConverter { get; } = new MonthBackgroundConverter();
        public static IValueConverter CurrentMonthForegroundConverter { get; } = new MonthForegroundConverter();

        private readonly TimeLogService _logService;
        private readonly TrackerService _trackerService;
        private PromptWindow? _currentPromptWindow;

        private DateTime _calendarCurrentMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _selectedDate = DateTime.Today;
        private double _previousWidth = 1060;
        private double _previousHeight = 740;
        private bool _isMiniMode = false;
        public const double DefaultPinnedWidth = 246;
        public const double DefaultPinnedHeight = 66;
        private double _pinnedWidth = DefaultPinnedWidth;
        private double _pinnedHeight = DefaultPinnedHeight;

        public MainWindow()
        {
            InitializeComponent();

            _logService = new TimeLogService();
            _trackerService = new TrackerService();

            _trackerService.PropertyChanged += (s, e) => Dispatcher.Invoke(UpdateTrackerUi);
            _trackerService.PromptRequested += OnPromptRequested;

            // Start tracker automatically on application launch
            _trackerService.Start();

            UpdateTrackerUi();
            UpdateDashboard();
            UpdateTodayOverview();
            UpdateCalendarView();
            UpdateDayEditor();

            Loaded += (s, e) =>
            {
                var args = Environment.GetCommandLineArgs();
                if (args.Any(a => a.Equals("--pinned", StringComparison.OrdinalIgnoreCase) || 
                                  a.Equals("--mini", StringComparison.OrdinalIgnoreCase)))
                {
                    OnPinToTopRightModeClick(this, new RoutedEventArgs());
                }
            };
        }

        private void OnPromptRequested()
        {
            Dispatcher.Invoke(ShowPromptDialog);
        }

        private void ShowPromptDialog()
        {
            if (_currentPromptWindow != null && _currentPromptWindow.IsVisible)
            {
                _currentPromptWindow.Activate();
                return;
            }

            _currentPromptWindow = new PromptWindow(_logService, _trackerService);
            _currentPromptWindow.Owner = this;
            _currentPromptWindow.LogAdded += () =>
            {
                UpdateDashboard();
                UpdateTodayOverview();
                UpdateCalendarView();
                UpdateDayEditor();
            };
            _currentPromptWindow.Closed += (s, e) => _currentPromptWindow = null;
            _currentPromptWindow.Show();
            _currentPromptWindow.Activate();
        }

        private void UpdateTrackerUi()
        {
            CountdownText.Text = _trackerService.CountdownDisplay;
            NavCountdownText.Text = _trackerService.CountdownDisplay;
            MiniCountdownText.Text = _trackerService.CountdownDisplay;
            CountdownSubtext.Text = $"remaining of {_trackerService.IntervalDisplay} block";
            CycleProgressBar.Value = _trackerService.ProgressPercent;
            MiniCycleProgressBar.Value = _trackerService.ProgressPercent;

            if (_trackerService.IsRunning)
            {
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B3A57"));
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC2FF"));
                NavStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC2FF"));
                MiniStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC2FF"));
                StatusBadgeText.Text = "ACTIVE";
                StatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC2FF"));

                BtnStart.IsEnabled = false;
                BtnPause.IsEnabled = true;
                MiniPlayPauseIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Pause24;
            }
            else
            {
                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2A1A"));
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA44"));
                NavStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA44"));
                MiniStatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA44"));
                StatusBadgeText.Text = "PAUSED";
                StatusBadgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA44"));

                BtnStart.IsEnabled = true;
                BtnPause.IsEnabled = false;
                MiniPlayPauseIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Play24;
            }
        }

        private void UpdateTodayOverview()
        {
            var todayTotal = _logService.GetTotalDurationToday();
            double todayHours = Math.Round(todayTotal.TotalHours, 1);
            int todayBlocks = (int)(todayTotal.TotalMinutes / 30);
            bool targetMet = todayTotal.TotalHours >= DaySummary.TargetHours;

            TodayTotalText.Text = $"Today: {todayHours:0.#}h / 7.5h ({todayBlocks} blocks)";

            if (targetMet)
            {
                TodayTargetBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));
                TodayTargetCheckText.Text = "✓";
                TodayTargetCheckText.Foreground = Brushes.White;
            }
            else
            {
                TodayTargetBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
                TodayTargetCheckText.Text = "⏱";
                TodayTargetCheckText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAAAAA"));
            }

            var weekTotal = _logService.GetTotalDurationForCurrentWeek();
            int weekHours = (int)weekTotal.TotalHours;
            int weekMins = weekTotal.Minutes;
            WeekTotalOverviewText.Text = $"This Week: {weekHours}h {weekMins:00}m total";
        }

        private void UpdateDashboard()
        {
            var (start, end) = _logService.GetCurrentWorkWeekRange();
            WeekRangeText.Text = $"Current Work Week: {start:ddd, MMM d} – {end:ddd, MMM d, yyyy}";

            var weekLogs = _logService.GetLogsForCurrentWorkWeek();
            var categorySummaries = _logService.GetCategorySummariesForCurrentWeek();
            var totalDuration = _logService.GetTotalDurationForCurrentWeek();

            // Total Hours & Blocks
            double totalHours = totalDuration.TotalHours;
            CardTotalHoursText.Text = $"{totalHours:F1} hrs";
            int totalBlocks = (int)(totalDuration.TotalMinutes / 30);
            CardTotalBlocksText.Text = $"{totalBlocks} check-in {(totalBlocks == 1 ? "block" : "blocks")} (30m ea)";

            // 7.5h Target Days Met
            int targetMetDays = 0;
            for (int i = 0; i < 7; i++)
            {
                var day = start.AddDays(i);
                if (_logService.GetTotalDurationForDate(day).TotalHours >= DaySummary.TargetHours)
                {
                    targetMetDays++;
                }
            }
            CardTargetDaysText.Text = $"{targetMetDays} of 5 work days";
            double targetPct = (targetMetDays / 5.0) * 100.0;
            CardTargetDaysPctText.Text = $"{targetPct:0}% work week completion";

            // Dominant Activity
            if (categorySummaries.Count > 0)
            {
                var top = categorySummaries[0];
                CardTopCategoryText.Text = top.Category;
                CardTopCategoryHoursText.Text = $"{top.FormattedDuration} ({top.PercentageDisplay})";
            }
            else
            {
                CardTopCategoryText.Text = "No activity yet";
                CardTopCategoryHoursText.Text = "0 hrs logged";
            }

            // Production Support & Incidents Ratio
            var supportCategories = new[]
            {
                "Production Incident & Triage",
                "Prod Support & Monitoring",
                "User Inquiries & On-Call Admin"
            };

            var supportLogs = weekLogs.Where(l => supportCategories.Any(sc => sc.Equals(l.Category, StringComparison.OrdinalIgnoreCase))).ToList();
            double supportMinutes = supportLogs.Sum(l => l.Duration.TotalMinutes);
            double totalMinutes = totalDuration.TotalMinutes;
            double supportHours = supportMinutes / 60.0;
            double supportPercent = totalMinutes > 0 ? (supportMinutes / totalMinutes) * 100.0 : 0;

            CardSupportRatioText.Text = $"{supportHours:F1}h ({supportPercent:0.#}%)";

            // Category Breakdown Items
            CategoryBreakdownItems.ItemsSource = categorySummaries;
            EmptyCategoriesNotice.Visibility = categorySummaries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Activity Log Items
            ActivityLogItems.ItemsSource = weekLogs;
            LogsCountText.Text = $"{weekLogs.Count} {(weekLogs.Count == 1 ? "entry" : "entries")}";
            EmptyLogsNotice.Visibility = weekLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================= CALENDAR & DAY EDITOR LOGIC =================
        private void UpdateCalendarView()
        {
            CalendarMonthTitle.Text = _calendarCurrentMonth.ToString("MMMM yyyy");
            var days = _logService.GetDaysSummaryForMonth(_calendarCurrentMonth.Year, _calendarCurrentMonth.Month, _selectedDate);
            CalendarDaysItems.ItemsSource = days;
        }

        private void UpdateDayEditor()
        {
            DayEditorTitleText.Text = _selectedDate.ToString("dddd, MMM d, yyyy");

            var dayLogs = _logService.GetLogsForDate(_selectedDate);
            var dayDuration = _logService.GetTotalDurationForDate(_selectedDate);
            double hours = Math.Round(dayDuration.TotalHours, 2);
            bool targetMet = hours >= DaySummary.TargetHours;

            DayLogsItems.ItemsSource = dayLogs;
            DayBlocksCountText.Text = $"{dayLogs.Count} {(dayLogs.Count == 1 ? "block" : "blocks")} ({hours:0.#}h)";
            EmptyDayLogsNotice.Visibility = dayLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            double pct = Math.Min((hours / DaySummary.TargetHours) * 100.0, 100.0);
            DayTargetProgressBar.Value = pct;
            DayTargetPctText.Text = $"{pct:0}%";

            if (targetMet)
            {
                DayTargetCardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C2E20"));
                DayTargetCardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D5A34"));
                DayTargetStatusText.Text = "✓ 7.5-Hour Target Achieved!";
                DayTargetStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#57D9A3"));
                DayTargetSubtext.Text = $"Total Logged: {hours:0.#}h ({dayLogs.Count} blocks)";
                DayTargetProgressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));
            }
            else
            {
                DayTargetCardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2514"));
                DayTargetCardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5A4826"));
                double remaining = Math.Max(DaySummary.TargetHours - hours, 0);
                DayTargetStatusText.Text = $"Incomplete ({remaining:0.#}h remaining for 7.5h target)";
                DayTargetStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA44"));
                DayTargetSubtext.Text = $"Logged so far: {hours:0.#}h of 7.5h target";
                DayTargetProgressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8C00"));
            }
        }

        private void OnCalendarDaySelected(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is DateTime date)
            {
                _selectedDate = date;
                UpdateCalendarView();
                UpdateDayEditor();
            }
        }

        private void OnPrevMonthClick(object sender, RoutedEventArgs e)
        {
            _calendarCurrentMonth = _calendarCurrentMonth.AddMonths(-1);
            UpdateCalendarView();
        }

        private void OnNextMonthClick(object sender, RoutedEventArgs e)
        {
            _calendarCurrentMonth = _calendarCurrentMonth.AddMonths(1);
            UpdateCalendarView();
        }

        private void OnTodayCalendarClick(object sender, RoutedEventArgs e)
        {
            _calendarCurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _selectedDate = DateTime.Today;
            UpdateCalendarView();
            UpdateDayEditor();
        }

        private void OnAddBlockForDayClick(object sender, RoutedEventArgs e)
        {
            var dialog = new EditLogDialog(_selectedDate);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.ResultLog != null)
            {
                _logService.AddLog(dialog.ResultLog);
                UpdateCalendarView();
                UpdateDayEditor();
                UpdateDashboard();
                UpdateTodayOverview();
            }
        }

        private void OnEditLogEntryClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is TimeLog log)
            {
                var dialog = new EditLogDialog(log.Timestamp.Date, log);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true && dialog.ResultLog != null)
                {
                    _logService.UpdateLog(dialog.ResultLog);
                    UpdateCalendarView();
                    UpdateDayEditor();
                    UpdateDashboard();
                    UpdateTodayOverview();
                }
            }
        }

        // ================= NAVIGATION & TAB SWITCHING =================
        private void OnNavTabChanged(object sender, RoutedEventArgs e)
        {
            if (TrackerView == null || DashboardView == null || CalendarView == null) return;

            if (TabTrackerRadio.IsChecked == true)
            {
                TrackerView.Visibility = Visibility.Visible;
                DashboardView.Visibility = Visibility.Collapsed;
                CalendarView.Visibility = Visibility.Collapsed;
            }
            else if (TabDashboardRadio.IsChecked == true)
            {
                TrackerView.Visibility = Visibility.Collapsed;
                DashboardView.Visibility = Visibility.Visible;
                CalendarView.Visibility = Visibility.Collapsed;
                UpdateDashboard();
            }
            else if (TabCalendarRadio.IsChecked == true)
            {
                TrackerView.Visibility = Visibility.Collapsed;
                DashboardView.Visibility = Visibility.Collapsed;
                CalendarView.Visibility = Visibility.Visible;
                UpdateCalendarView();
                UpdateDayEditor();
            }
        }

        private void OnStartClick(object sender, RoutedEventArgs e) => _trackerService.Start();
        private void OnPauseClick(object sender, RoutedEventArgs e) => _trackerService.Pause();
        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            _trackerService.Reset();
            UpdateTrackerUi();
        }

        private void OnPromptNowClick(object sender, RoutedEventArgs e) => ShowPromptDialog();

        // ================= MINI TIMER MODE ("JUST THE TIMER") =================
        private void OnPinToTopRightModeClick(object sender, RoutedEventArgs e)
        {
            OnSwitchToMiniModeClick(sender, e);
            SnapToTopRight();
        }

        private void OnSwitchToMiniModeClick(object sender, RoutedEventArgs e)
        {
            _previousWidth = Width;
            _previousHeight = Height;
            _isMiniMode = true;

            MinWidth = 200;
            MinHeight = 48;
            Width = _pinnedWidth;
            Height = _pinnedHeight;
            Topmost = true;

            FullModeGrid.Visibility = Visibility.Collapsed;
            MiniModeBorder.Visibility = Visibility.Visible;
            UpdateTrackerUi();
            Focus();
        }

        private void SnapToTopRight()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 14;
            Top = workArea.Top + 10;
            Topmost = true;
        }

        private void OnSnapToTopRightClick(object sender, RoutedEventArgs e)
        {
            SnapToTopRight();
        }

        private void OnSwitchToFullModeClick(object sender, RoutedEventArgs e)
        {
            _isMiniMode = false;

            MiniModeBorder.Visibility = Visibility.Collapsed;
            FullModeGrid.Visibility = Visibility.Visible;

            MinWidth = 940;
            MinHeight = 660;
            Width = Math.Max(_previousWidth, 980);
            Height = Math.Max(_previousHeight, 680);
            Topmost = false;

            CenterOnScreen();

            UpdateTrackerUi();
            Focus();
        }

        private void CenterOnScreen()
        {
            var workArea = SystemParameters.WorkArea;
            Left = Math.Max(workArea.Left, workArea.Left + (workArea.Width - Width) / 2);
            Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - Height) / 2);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == Key.Escape && _isMiniMode)
            {
                OnSwitchToFullModeClick(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_isMiniMode && sizeInfo.NewSize.Width >= 180 && sizeInfo.NewSize.Height >= 40)
            {
                _pinnedWidth = sizeInfo.NewSize.Width;
                _pinnedHeight = sizeInfo.NewSize.Height;
            }
        }

        private void OnMiniModeDrag(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void OnMiniPlayPauseClick(object sender, RoutedEventArgs e)
        {
            if (_trackerService.IsRunning)
                _trackerService.Pause();
            else
                _trackerService.Start();
        }

        private void OnRefreshDashboardClick(object sender, RoutedEventArgs e)
        {
            UpdateDashboard();
            UpdateTodayOverview();
            UpdateCalendarView();
            UpdateDayEditor();
        }

        private void OnIntervalSelectorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_trackerService == null) return;

            if (IntervalSelector.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            {
                if (double.TryParse(tagStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double minutes))
                {
                    _trackerService.Interval = TimeSpan.FromMinutes(minutes);
                    _trackerService.Start();
                    UpdateTrackerUi();
                }
            }
        }

        private void OnDeleteLogClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Guid id)
            {
                var result = System.Windows.MessageBox.Show(
                    "Are you sure you want to delete this log entry?",
                    "Delete Entry",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    _logService.DeleteLog(id);
                    UpdateDashboard();
                    UpdateTodayOverview();
                    UpdateCalendarView();
                    UpdateDayEditor();
                }
            }
        }

        // ================= EXCEL EXPORT =================
        private void OnExportWeekToExcelClick(object sender, RoutedEventArgs e)
        {
            var (start, end) = _logService.GetWeekRangeForDate(_selectedDate);

            var saveDialog = new SaveFileDialog
            {
                Title = "Export Work Week to Excel",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"WorkTimeTracker_Week_{start:yyyy-MM-dd}.xlsx",
                DefaultExt = ".xlsx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    ExcelExportService.ExportWeekToExcel(_logService, start, saveDialog.FileName);

                    var openResult = System.Windows.MessageBox.Show(
                        $"Week successfully exported to:\n{saveDialog.FileName}\n\nWould you like to open it now in Excel?",
                        "Export Complete",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (openResult == System.Windows.MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error exporting to Excel: {ex.Message}",
                        "Export Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    // ================= VALUE CONVERTERS =================
    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is string s && !string.IsNullOrWhiteSpace(s)) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class SelectedThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? new Thickness(2) : new Thickness(1);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class SelectedBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D4")) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#282828"));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class MonthBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1C1C1C")) 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#121212"));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class MonthForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) 
                ? Brushes.White 
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555"));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}