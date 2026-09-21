using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WorkTimeTracker.Models;
using WorkTimeTracker.Services;
using Wpf.Ui.Controls;

namespace WorkTimeTracker
{
    public partial class EditLogDialog : FluentWindow
    {
        public TimeLog? ResultLog { get; private set; }
        private readonly DateTime _targetDate;
        private readonly Guid _existingId;

        public EditLogDialog(DateTime targetDate, TimeLog? existingLog = null)
        {
            InitializeComponent();

            CategoryCombo.ItemsSource = TimeLogService.DefaultCategories;

            if (existingLog != null)
            {
                _existingId = existingLog.Id;
                _targetDate = existingLog.Timestamp.Date;

                DialogTitleText.Text = "EDIT TIME BLOCK";
                DialogDateSubtitle.Text = $"For {existingLog.Timestamp:dddd, MMM d, yyyy}";
                BtnSave.Content = "Update Entry";

                CategoryCombo.Text = existingLog.Category;
                CategoryCombo.SelectedItem = existingLog.Category;
                TimeBox.Text = existingLog.Timestamp.ToString("HH:mm");

                // Set duration combo
                int minutes = (int)existingLog.Duration.TotalMinutes;
                SelectDuration(minutes);

                DescriptionBox.Text = existingLog.Description;
            }
            else
            {
                _existingId = Guid.NewGuid();
                _targetDate = targetDate.Date;

                DialogTitleText.Text = "ADD NEW TIME BLOCK";
                DialogDateSubtitle.Text = $"For {targetDate:dddd, MMM d, yyyy}";
                BtnSave.Content = "Add Entry";

                CategoryCombo.SelectedIndex = 0;
                TimeBox.Text = DateTime.Now.ToString("HH:mm");
                DurationCombo.SelectedIndex = 0; // 30 mins
            }
        }

        private void SelectDuration(int minutes)
        {
            foreach (ComboBoxItem item in DurationCombo.Items)
            {
                if (item.Tag is string tag && int.TryParse(tag, out int val) && val == minutes)
                {
                    DurationCombo.SelectedItem = item;
                    return;
                }
            }
            // If custom duration not in items, select default
            DurationCombo.SelectedIndex = 0;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string category = CategoryCombo.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(category))
            {
                category = "Feature Development & Coding";
            }

            // Parse time
            int hour = DateTime.Now.Hour;
            int minute = (DateTime.Now.Minute / 30) * 30;

            if (TimeSpan.TryParseExact(TimeBox.Text?.Trim(), new[] { "h\\:mm", "hh\\:mm", "H\\:mm", "HH\\:mm" }, CultureInfo.InvariantCulture, out TimeSpan parsedTime))
            {
                hour = parsedTime.Hours;
                minute = parsedTime.Minutes;
            }

            var fullTimestamp = new DateTime(_targetDate.Year, _targetDate.Month, _targetDate.Day, hour, minute, 0);

            int durationMinutes = 30;
            if (DurationCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag && int.TryParse(tag, out int parsedMinutes))
            {
                durationMinutes = parsedMinutes;
            }

            ResultLog = new TimeLog
            {
                Id = _existingId,
                Timestamp = fullTimestamp,
                Duration = TimeSpan.FromMinutes(durationMinutes),
                Category = category,
                Description = DescriptionBox.Text?.Trim() ?? string.Empty
            };

            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
