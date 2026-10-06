using System;
using System.Threading;
using System.Windows;
using SecApper.Security.Updates;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace SecApper.FolderLocker.Views;

public partial class UpdateAvailableDialog : Window
{
    private readonly IUpdateService _updateService;
    private readonly UpdateInfo _updateInfo;
    private CancellationTokenSource? _cts;

    public UpdateAvailableDialog(IUpdateService updateService, UpdateInfo updateInfo)
    {
        InitializeComponent();
        _updateService = updateService;
        _updateInfo = updateInfo;

        CurrentVerText.Text = $"v{_updateService.CurrentVersion}";
        NewVerText.Text = $"v{_updateInfo.Version}";
        ReleaseNotesText.Text = !string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes) 
            ? _updateInfo.ReleaseNotes 
            : "Performance enhancements, security updates, and bug fixes.";

        if (_updateInfo.Mandatory)
        {
            LaterButton.Visibility = Visibility.Collapsed;
        }
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        UpdateNowButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        _cts = new CancellationTokenSource();

        var progress = new Progress<UpdateProgress>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                DownloadProgressBar.Value = p.Percentage;
                ProgressPercentText.Text = $"{p.Percentage}%";
                ProgressStatusText.Text = p.StatusMessage;
            });
        });

        try
        {
            // 1. Download
            string downloadedPath = await _updateService.DownloadUpdateAsync(_updateInfo, progress, _cts.Token);

            // 2. Verify
            ProgressStatusText.Text = "Verifying package integrity (SHA-256)...";
            bool isVerified = _updateService.VerifyUpdatePackage(downloadedPath, _updateInfo, out string? verifyError);

            if (!isVerified)
            {
                MessageBox.Show(
                    $"Update package verification failed:\n\n{verifyError}\n\nThe download has been discarded to protect your system.",
                    "Update Verification Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ProgressPanel.Visibility = Visibility.Collapsed;
                UpdateNowButton.IsEnabled = true;
                LaterButton.IsEnabled = true;
                return;
            }

            ProgressStatusText.Text = "Launching secure updater and applying update...";

            // 3. Launch Updater & Shutdown application cleanly
            bool launched = await _updateService.LaunchUpdaterAndExitAsync(downloadedPath, _updateInfo);
            if (launched)
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.ShutdownApp();
                }
                else
                {
                    Environment.Exit(0);
                }
            }
            else
            {
                MessageBox.Show("Could not launch the update installer.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ProgressPanel.Visibility = Visibility.Collapsed;
                UpdateNowButton.IsEnabled = true;
                LaterButton.IsEnabled = true;
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "Download cancelled.";
            UpdateNowButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to download or apply update:\n\n{ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ProgressPanel.Visibility = Visibility.Collapsed;
            UpdateNowButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }
}
