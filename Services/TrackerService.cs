using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace WorkTimeTracker.Services
{
    public class TrackerService : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer;
        private TimeSpan _interval = TimeSpan.FromMinutes(30);
        private TimeSpan _timeRemaining;
        private bool _isRunning = true;
        private DateTime? _snoozeUntil;
        private DateTime? _lastTriggeredBoundary;
        private DateTime? _currentTargetBoundary;

        public event Action? PromptRequested;

        public TrackerService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;

            // Immediately link and synchronize to real wall-clock half-hour boundary
            _currentTargetBoundary = GetNextHalfHourBoundary(DateTime.Now);
            UpdateTimeRemainingFromClock();
        }

        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                if (_interval != value)
                {
                    _interval = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IntervalDisplay));
                }
            }
        }

        public TimeSpan TimeRemaining
        {
            get => _timeRemaining;
            private set
            {
                if (_timeRemaining != value)
                {
                    _timeRemaining = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CountdownDisplay));
                    OnPropertyChanged(nameof(ProgressPercent));
                    OnPropertyChanged(nameof(ProgressFraction));
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string CountdownDisplay
        {
            get
            {
                if (_timeRemaining.TotalHours >= 1)
                    return _timeRemaining.ToString(@"hh\:mm\:ss");
                return _timeRemaining.ToString(@"mm\:ss");
            }
        }

        public double ProgressPercent
        {
            get
            {
                if (_interval.TotalSeconds <= 0) return 0;
                double elapsed = _interval.TotalSeconds - _timeRemaining.TotalSeconds;
                return Math.Clamp((elapsed / _interval.TotalSeconds) * 100.0, 0, 100);
            }
        }

        public double ProgressFraction => ProgressPercent / 100.0;

        public string IntervalDisplay => $"{_interval.TotalMinutes:0} minutes";

        public string StatusText => _isRunning ? "Active (Tracking)" : "Paused";

        public void Start()
        {
            if (!_isRunning)
            {
                IsRunning = true;
                UpdateTimeRemainingFromClock();
                _timer.Start();
            }
            else if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }

        public void Pause()
        {
            if (_isRunning)
            {
                IsRunning = false;
            }
        }

        public void Reset()
        {
            _snoozeUntil = null;
            _interval = TimeSpan.FromMinutes(30);
            UpdateTimeRemainingFromClock();
        }

        public void Snooze(TimeSpan duration)
        {
            _snoozeUntil = DateTime.Now.Add(duration);
            _interval = duration;
            TimeRemaining = duration;
            IsRunning = true;
            if (!_timer.IsEnabled) _timer.Start();
        }

        public void TriggerPromptNow()
        {
            PromptRequested?.Invoke();
            _snoozeUntil = null;
            _interval = TimeSpan.FromMinutes(30);
            UpdateTimeRemainingFromClock();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            DateTime now = DateTime.Now;

            // Handle active snooze
            if (_snoozeUntil.HasValue)
            {
                var diff = _snoozeUntil.Value - now;
                if (diff <= TimeSpan.Zero)
                {
                    _snoozeUntil = null;
                    _interval = TimeSpan.FromMinutes(30);
                    PromptRequested?.Invoke();
                    UpdateTimeRemainingFromClock();
                }
                else
                {
                    TimeRemaining = TimeSpan.FromSeconds(Math.Ceiling(diff.TotalSeconds));
                }
                return;
            }

            // Real wall-clock half-hour boundary tracking (:00 and :30)
            var nextBoundary = GetNextHalfHourBoundary(now);

            // Check if boundary was crossed
            if (_currentTargetBoundary.HasValue && now >= _currentTargetBoundary.Value)
            {
                if (_lastTriggeredBoundary != _currentTargetBoundary.Value)
                {
                    _lastTriggeredBoundary = _currentTargetBoundary.Value;
                    PromptRequested?.Invoke();
                }
            }

            _currentTargetBoundary = nextBoundary;
            var remainingDiff = nextBoundary - now;
            var seconds = Math.Max(0, Math.Ceiling(remainingDiff.TotalSeconds));
            TimeRemaining = TimeSpan.FromSeconds(seconds);
        }

        private void UpdateTimeRemainingFromClock()
        {
            DateTime now = DateTime.Now;
            var nextBoundary = GetNextHalfHourBoundary(now);
            _currentTargetBoundary = nextBoundary;
            var remainingDiff = nextBoundary - now;
            var seconds = Math.Max(0, Math.Ceiling(remainingDiff.TotalSeconds));
            TimeRemaining = TimeSpan.FromSeconds(seconds);
        }

        public static DateTime GetNextHalfHourBoundary(DateTime now)
        {
            if (now.Minute < 30)
            {
                return new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0, now.Kind);
            }
            else
            {
                return new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind).AddHours(1);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
