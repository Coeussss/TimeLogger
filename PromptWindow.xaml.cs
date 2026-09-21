using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using WorkTimeTracker.Models;
using WorkTimeTracker.Services;
using Wpf.Ui.Controls;

namespace WorkTimeTracker
{
    public partial class PromptWindow : FluentWindow
    {
        private readonly TimeLogService _logService;
        private readonly TrackerService? _trackerService;

        public event Action? LogAdded;

        public PromptWindow(TimeLogService logService, TrackerService? trackerService = null)
        {
            InitializeComponent();
            _logService = logService;
            _trackerService = trackerService;

            CategoryComboBox.ItemsSource = TimeLogService.DefaultCategories;
            CategoryComboBox.SelectedIndex = 0;

            Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemSounds.Asterisk.Play();
            }
            catch
            {
                // Ignore audio playback issues on headless or muted systems
            }

            DescriptionBox.Focus();
        }

        private void OnCategoryChipClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string category)
            {
                CategoryComboBox.Text = category;
                CategoryComboBox.SelectedItem = category;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string category = CategoryComboBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "Feature Development & Coding";
            }

            string description = DescriptionBox.Text?.Trim() ?? string.Empty;

            var log = new TimeLog
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.Now,
                Duration = TimeSpan.FromMinutes(30),
                Category = category,
                Description = description
            };

            _logService.AddLog(log);
            LogAdded?.Invoke();
            Close();
        }

        private void OnSnoozeClick(object sender, RoutedEventArgs e)
        {
            _trackerService?.Snooze(TimeSpan.FromMinutes(5));
            Close();
        }

        private void OnSkipClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
