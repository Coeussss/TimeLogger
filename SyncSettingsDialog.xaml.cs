using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WorkTimeTracker.Services;

namespace WorkTimeTracker
{
    public partial class SyncSettingsDialog : FluentWindow
    {
        private readonly TimeLogService _logService;
        public bool ConfigurationChanged { get; private set; }

        public SyncSettingsDialog(TimeLogService logService)
        {
            InitializeComponent();
            _logService = logService;

            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            TxtServerUrl.Text = _logService.ServerUrl;
            TxtApiKey.Text = _logService.ApiKey;
            TxtDriveFolder.Text = _logService.CurrentSyncMode == SyncMode.GoogleDrive ? _logService.CurrentStorageDirectory : string.Empty;

            switch (_logService.CurrentSyncMode)
            {
                case SyncMode.HomeServer:
                    RadioHomeServer.IsChecked = true;
                    break;
                case SyncMode.GoogleDrive:
                    RadioGoogleDrive.IsChecked = true;
                    break;
                default:
                    RadioLocalOnly.IsChecked = true;
                    break;
            }

            UpdatePanelsVisibility();
        }

        private void OnSyncModeChanged(object sender, RoutedEventArgs e)
        {
            UpdatePanelsVisibility();
        }

        private void UpdatePanelsVisibility()
        {
            if (PanelHomeServer == null || PanelGoogleDrive == null) return;

            PanelHomeServer.Visibility = RadioHomeServer.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelGoogleDrive.Visibility = RadioGoogleDrive.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnTestServerConnectionClick(object sender, RoutedEventArgs e)
        {
            TxtTestStatus.Text = "Testing connection...";
            TxtTestStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CC2FF"));
            BtnTestServer.IsEnabled = false;

            try
            {
                string url = TxtServerUrl.Text.Trim();
                string key = TxtApiKey.Text.Trim();

                var result = await _logService.TestServerConnectionAsync(url, key);
                if (result.Success)
                {
                    TxtTestStatus.Text = "✓ Connected & Authorized!";
                    TxtTestStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#107C41"));
                }
                else
                {
                    TxtTestStatus.Text = $"✗ {result.Message}";
                    TxtTestStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"));
                }
            }
            finally
            {
                BtnTestServer.IsEnabled = true;
            }
        }

        private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Google Drive or Cloud Sync Folder",
                InitialDirectory = Directory.Exists(TxtDriveFolder.Text) 
                    ? TxtDriveFolder.Text 
                    : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                TxtDriveFolder.Text = dialog.FolderName;

                // Auto copy APK if found
                try
                {
                    string[] candidatePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WorkTimeTracker.apk"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\WorkTimeTracker.apk"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\WorkTimeTracker.apk"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\android\app\build\outputs\apk\debug\app-debug.apk")
                    };

                    string? foundApk = candidatePaths.FirstOrDefault(p => File.Exists(Path.GetFullPath(p)));
                    if (foundApk != null)
                    {
                        string apkDest = Path.Combine(dialog.FolderName, "WorkTimeTracker.apk");
                        File.Copy(Path.GetFullPath(foundApk), apkDest, true);
                    }
                }
                catch { }
            }
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (RadioHomeServer.IsChecked == true)
            {
                string url = TxtServerUrl.Text.Trim();
                string key = TxtApiKey.Text.Trim();

                if (string.IsNullOrWhiteSpace(url))
                {
                    System.Windows.MessageBox.Show("Please enter your Home Server URL.", "Missing URL", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                await _logService.ConfigureHomeServerAsync(url, key);
                ConfigurationChanged = true;
            }
            else if (RadioGoogleDrive.IsChecked == true)
            {
                string folder = TxtDriveFolder.Text.Trim();
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    System.Windows.MessageBox.Show("Please select a valid folder path.", "Missing Folder", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                _logService.ConfigureGoogleDrive(folder);
                ConfigurationChanged = true;
            }
            else
            {
                _logService.ResetToLocal();
                ConfigurationChanged = true;
            }

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
