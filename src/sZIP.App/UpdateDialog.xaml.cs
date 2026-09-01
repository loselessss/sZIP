using L = sZIP.App.Localization;
using System.Diagnostics;
using System.Windows;
using sZIP.Application;

namespace sZIP.App;

public partial class UpdateDialog : Window
{
    private readonly GitHubUpdateService _service;
    private readonly AvailableUpdate _update;
    private CancellationTokenSource? _downloadCancellation;

    public UpdateDialog(GitHubUpdateService service, AvailableUpdate update)
    {
        // Keep every label and button in the same language as the selected release notes.
        L.Apply(service.ReleaseNotesLanguage);
        InitializeComponent();
        _service = service;
        _update = update;
        TitleText.Text = L.F("UpdateAvailable", update.Version);
        CurrentVersionText.Text = service.CurrentVersion.ToString();
        NewVersionText.Text = update.Version.ToString();
        InstallerText.Text = update.Asset is null
            ? L.T("PendingUpload")
            : $"{update.Asset.Name} ({FormatSize(update.Asset.Size)})";
        NotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? L.T("NoNotes")
            : update.ReleaseNotes;

        if (update.Asset is null)
        {
            InstallButton.IsEnabled = false;
            StatusText.Text = L.T("NoInstallerYet");
        }
        else if (string.IsNullOrEmpty(update.Asset.Sha256))
        {
            InstallButton.IsEnabled = false;
            StatusText.Text = L.T("NoIntegrity");
        }
    }

    public string? InstallerPath { get; private set; }
    public bool SkipVersionRequested { get; private set; }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        SetDownloading(true);
        _downloadCancellation = new CancellationTokenSource();
        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            if (value.TotalBytes > 0)
            {
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = Math.Min(100, value.CompletedBytes * 100d / value.TotalBytes);
            }
            else
            {
                DownloadProgress.IsIndeterminate = true;
            }
            StatusText.Text = $"{value.CompletedBytes / 1048576d:F1} MB"
                + (value.TotalBytes > 0 ? $" / {value.TotalBytes / 1048576d:F1} MB" : string.Empty)
                + $" · {value.BytesPerSecond / 1048576d:F1} MB/s";
        });

        try
        {
            InstallerPath = await _service.DownloadAsync(_update, progress, _downloadCancellation.Token);
            _downloadCancellation.Dispose();
            _downloadCancellation = null;
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 100;
            StatusText.Text = L.T("StartingInstall");
            DialogResult = true;
        }
        catch (UpdateCancelledException exception)
        {
            StatusText.Text = L.Error(exception.Message);
        }
        catch (Exception exception)
        {
            StatusText.Text = L.Error(exception.Message);
            System.Windows.MessageBox.Show(this, L.Error(exception.Message), L.T("DownloadFailed"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _downloadCancellation?.Dispose();
            _downloadCancellation = null;
            if (DialogResult != true) SetDownloading(false);
        }
    }

    private void SetDownloading(bool downloading)
    {
        DownloadProgress.Visibility = downloading ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = !downloading && _update.Asset is not null
            && !string.IsNullOrEmpty(_update.Asset.Sha256);
        ReleasePageButton.IsEnabled = !downloading;
        SkipButton.IsEnabled = !downloading;
        LaterButton.Content = downloading ? L.T("Cancel") : L.T("Later");
    }

    private void ReleasePageButton_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(_update.ReleaseUrl.AbsoluteUri) { UseShellExecute = true });

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this,
                L.F("SkipUpdatePrompt", _update.Version),
                L.T("SkipUpdate"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        SkipVersionRequested = true;
        DialogResult = false;
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadCancellation is not null)
        {
            StatusText.Text = L.T("CancelingDownload");
            _downloadCancellation.Cancel();
            return;
        }
        DialogResult = false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_downloadCancellation is not null)
        {
            e.Cancel = true;
            _downloadCancellation.Cancel();
        }
        base.OnClosing(e);
    }

    private static string FormatSize(long size) => size <= 0 ? L.T("UnknownSize") : $"{size / 1048576d:F1} MB";
}
