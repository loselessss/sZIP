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
        InitializeComponent();
        _service = service;
        _update = update;
        TitleText.Text = $"sZIP {update.Version} 업데이트가 있습니다.";
        CurrentVersionText.Text = service.CurrentVersion.ToString();
        NewVersionText.Text = update.Version.ToString();
        InstallerText.Text = update.Asset is null
            ? "등록 대기 중"
            : $"{update.Asset.Name} ({FormatSize(update.Asset.Size)})";
        NotesText.Text = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? "변경 기록이 없습니다."
            : update.ReleaseNotes;

        if (update.Asset is null)
        {
            InstallButton.IsEnabled = false;
            StatusText.Text = "이 릴리스에는 아직 Windows 설치 파일이 없습니다.";
        }
        else if (string.IsNullOrEmpty(update.Asset.Sha256))
        {
            InstallButton.IsEnabled = false;
            StatusText.Text = "설치 파일 무결성 정보가 없어 자동 설치할 수 없습니다.";
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
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 100;
            StatusText.Text = "SHA-256 검증을 마쳤습니다. 설치를 시작합니다.";
            DialogResult = true;
        }
        catch (UpdateCancelledException exception)
        {
            StatusText.Text = exception.Message;
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            System.Windows.MessageBox.Show(this, exception.Message, "업데이트 다운로드 실패",
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
        LaterButton.Content = downloading ? "취소" : "나중에";
    }

    private void ReleasePageButton_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(_update.ReleaseUrl.AbsoluteUri) { UseShellExecute = true });

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this,
                $"v{_update.Version} 알림을 자동으로 다시 표시하지 않을까요?\n수동 업데이트 확인은 언제든 사용할 수 있습니다.",
                "업데이트 건너뛰기", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
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
            StatusText.Text = "다운로드를 취소하는 중입니다…";
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

    private static string FormatSize(long size) => size <= 0 ? "크기 정보 없음" : $"{size / 1048576d:F1} MB";
}
