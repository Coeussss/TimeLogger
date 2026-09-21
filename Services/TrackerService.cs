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
        private bool _isRunning;
        private DateTime _cycleStartTime;

        public event Action? PromptRequested;

        public TrackerService()
        {
            _timeRemaining = _interval;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
        }

        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                if (_interval != value)
                {
                    _interval = value;
                    Reset();
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
                _cycleStartTime = DateTime.Now;
                _timer.Start();
                IsRunning = true;
            }
        }

        public void Pause()
        {
            if (_isRunning)
            {
                _timer.Stop();
                IsRunning = false;
            }
        }

        public void Reset()
        {
            _timer.Stop();
            IsRunning = false;
            TimeRemaining = _interval;
        }

        public void Snooze(TimeSpan duration)
        {
            _timer.Stop();
            TimeRemaining = duration;
            _interval = duration;
            _timer.Start();
            IsRunning = true;
        }

        public void TriggerPromptNow()
        {
            PromptRequested?.Invoke();
            // Reset for next cycle
            _interval = TimeSpan.FromMinutes(30);
            TimeRemaining = _interval;
            if (_isRunning)
            {
                _cycleStartTime = DateTime.Now;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (TimeRemaining > TimeSpan.FromSeconds(1))
            {
                TimeRemaining -= TimeSpan.FromSeconds(1);
            }
            else
            {
                TimeRemaining = TimeSpan.Zero;
                // Interval completed! Trigger prompt
                PromptRequested?.Invoke();
                // Reset interval back to standard 30 minutes if it was snoozed
                _interval = TimeSpan.FromMinutes(30);
                TimeRemaining = _interval;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
