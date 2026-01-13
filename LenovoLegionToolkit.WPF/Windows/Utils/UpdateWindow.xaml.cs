using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

public partial class UpdateWindow : IProgress<float>
{
    private readonly UpdateChecker _updateChecker = IoCContainer.Resolve<UpdateChecker>();
    private readonly UpdateCheckSettings _updateCheckSettings = IoCContainer.Resolve<UpdateCheckSettings>();

    private CancellationTokenSource? _downloadCancellationTokenSource;
    private Version? _latestVersion;

    public UpdateWindow() => InitializeComponent();

    private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var updates = await _updateChecker.GetUpdatesAsync();
        
        if (updates.Length > 0)
        {
            _latestVersion = updates.First().Version;
            
            // Update version badge
            _versionText.Text = $"v{_latestVersion}";
            
            // Update info text
            var currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            _updateInfoText.Text = $"Update from v{currentVersion?.Major}.{currentVersion?.Minor}.{currentVersion?.Build} → v{_latestVersion}";
        }

        var stringBuilder = new StringBuilder();
        foreach (var update in updates)
        {
            stringBuilder.AppendLine("**" + update.Title + "**   _(" + update.Date.ToString("D") + ")_")
                .AppendLine()
                .AppendLine(update.Description)
                .AppendLine();
        }

        _markdownViewer.Markdown = stringBuilder.ToString();

        _downloadButton.IsEnabled = true;
        _remindLaterButton.IsEnabled = true;
        _skipVersionButton.IsEnabled = true;
    }

    private void UpdateWindow_Closing(object? sender, CancelEventArgs e) => _downloadCancellationTokenSource?.Cancel();

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_downloadCancellationTokenSource is not null)
                await _downloadCancellationTokenSource.CancelAsync();

            _downloadCancellationTokenSource = new();

            SetDownloading(true);

            var path = await _updateChecker.DownloadLatestUpdateAsync(this, _downloadCancellationTokenSource.Token);

            _downloadCancellationTokenSource = null;

            // Clear any skipped version or remind later
            _updateCheckSettings.Store.SkippedVersion = null;
            _updateCheckSettings.Store.RemindLaterDateTime = null;
            _updateCheckSettings.SynchronizeStore();

            Process.Start(path, $"/SILENT /RESTARTAPPLICATIONS /LANG={Resource.Culture.Name.Replace("-", string.Empty)}");
            await App.Current.ShutdownAsync();
        }
        catch (OperationCanceledException)
        {
            SetDownloading(false);
        }
        catch
        {
            SetDownloading(false);

            Constants.LatestReleaseUri.Open();
            Close();
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) => _downloadCancellationTokenSource?.Cancel();

    private void RemindLaterButton_Click(object sender, RoutedEventArgs e)
    {
        // Set remind later to configured hours (default 24)
        var remindHours = _updateCheckSettings.Store.RemindLaterHours;
        _updateCheckSettings.Store.RemindLaterDateTime = DateTime.UtcNow.AddHours(remindHours);
        _updateCheckSettings.SynchronizeStore();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Remind later set for {remindHours} hours");

        Close();
    }

    private void SkipVersionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestVersion is not null)
        {
            // Skip this specific version
            _updateCheckSettings.Store.SkippedVersion = _latestVersion.ToString();
            _updateCheckSettings.Store.RemindLaterDateTime = null; // Clear remind later
            _updateCheckSettings.SynchronizeStore();

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Skipped version: {_latestVersion}");
        }

        Close();
    }

    private void SetDownloading(bool isDownloading)
    {
        if (isDownloading)
        {
            _progressSection.Visibility = Visibility.Visible;
            _downloadStatusText.Text = "Downloading update...";

            _downloadButton.Visibility = Visibility.Collapsed;
            _downloadButton.IsEnabled = false;

            _cancelDownloadButton.Visibility = Visibility.Visible;
            _cancelDownloadButton.IsEnabled = true;

            _secondaryActions.Visibility = Visibility.Collapsed;
        }
        else
        {
            _downloadProgressBar.Value = 0;
            _downloadProgressBar.IsIndeterminate = true;
            _progressSection.Visibility = Visibility.Collapsed;

            _downloadButton.Visibility = Visibility.Visible;
            _downloadButton.IsEnabled = true;

            _cancelDownloadButton.Visibility = Visibility.Collapsed;
            _cancelDownloadButton.IsEnabled = false;

            _secondaryActions.Visibility = Visibility.Visible;
        }
    }

    public void Report(float value) => Dispatcher.BeginInvoke(() =>
    {
        _downloadProgressBar.IsIndeterminate = !(value > 0);
        _downloadProgressBar.Value = value;

        if (value > 0)
        {
            var percentage = (int)(value * 100);
            _downloadStatusText.Text = $"Downloading... {percentage}%";
        }
    });
}
