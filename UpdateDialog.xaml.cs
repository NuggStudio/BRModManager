// UpdateDialog.xaml.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace BrickRigsModManager
{
    public partial class UpdateDialog : Window
    {
        private readonly ModManagerVersionInfo _versionInfo;
        private readonly string _currentVersion;
        private readonly UpdateService _updateService;
        private readonly Action<string> _logAction;
        private CancellationTokenSource _cancellationTokenSource;

        public UpdateDialog(ModManagerVersionInfo versionInfo, string currentVersion, UpdateService updateService, Action<string> logAction = null)
        {
            InitializeComponent();

            _versionInfo = versionInfo;
            _currentVersion = currentVersion;
            _updateService = updateService;
            _logAction = logAction;

            // Initialize the dialog
            InitializeDialog();
        }

        private void LogDebug(string message)
        {
            _logAction?.Invoke(message);
        }

        private void InitializeDialog()
        {
            // Set version information
            CurrentVersionText.Text = $"Current Version: {_currentVersion}";
            NewVersionText.Text = $"New Version: {_versionInfo.Version}";

            // Format release date
            var releaseDate = _versionInfo.ReleaseDate.ToString("MMMM d, yyyy");
            ReleaseDateText.Text = $"Released: {releaseDate}";

            // Set what's new list
            WhatsNewList.ItemsSource = _versionInfo.WhatsNew;

            // Set critical update info if applicable
            if (_versionInfo.IsCriticalUpdate)
            {
                VersionInfoText.Text = "A critical update is available for Brick Rigs Mod Manager.";
                VersionInfoText.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                RemindLaterButton.Content = "Update Later";
            }
        }

        private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            DialogResult = false;
            Close();
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable buttons
                UpdateNowButton.IsEnabled = false;
                RemindLaterButton.Content = "Cancel";

                // Show progress panel
                ProgressPanel.Visibility = Visibility.Visible;

                // Create progress reporter
                var progress = new Progress<(int percentage, string status)>(ReportProgress);

                // Create cancellation token
                _cancellationTokenSource = new CancellationTokenSource();

                // Download the update
                var updateFilePath = await _updateService.DownloadUpdate(
                    _versionInfo,
                    Path.GetTempPath(),
                    progress);

                // Check if cancelled
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    LogDebug("Update cancelled by user");
                    return;
                }

                // Install the update
                await _updateService.InstallUpdate(updateFilePath, AppDomain.CurrentDomain.BaseDirectory, progress);

                // The application will exit during the install process
            }
            catch (OperationCanceledException)
            {
                LogDebug("Update cancelled");
                StatusText.Text = "Update cancelled.";

                // Re-enable update button
                UpdateNowButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                LogDebug($"Error during update: {ex.Message}");

                MessageBox.Show(
                    $"An error occurred during the update:\n\n{ex.Message}\n\nPlease try again later or download the update manually.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Re-enable update button
                UpdateNowButton.IsEnabled = true;
            }
        }

        private void ReportProgress((int percentage, string status) progress)
        {
            ProgressBar.Value = progress.percentage;
            ProgressText.Text = progress.status;
            StatusText.Text = progress.status;
        }
    }
}
