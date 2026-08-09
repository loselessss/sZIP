using System.IO;
using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using sZIP.Application;
using sZIP.Archive;
using sZIP.Domain;
using sZIP.Watcher;

namespace sZIP.App;

public partial class MainWindow : Window
{
    private readonly MultiFormatArchiveService _manualExtractionService = new();
    private readonly ArchiveWorkspace _workspace;
    private readonly ZipArchiveService _manualArchiveService = new();
    private readonly SevenZipArchiveService _sevenZipArchiveService = new();
    private readonly MultiFormatArchiveService _automaticArchiveService = new();
    private readonly SemaphoreSlim _automaticExtractionLock = new(1, 1);
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private CancellationTokenSource? _operationCancellation;
    private RecursiveArchiveWatcher? _automaticWatcher;
    private bool _allowExit;

    public event EventHandler? AutoExtractEnabledChanged;
    public event EventHandler? HiddenToTray;

    public bool IsAutoExtractEnabled
    {
        get => AutoExtractCheckBox.IsChecked == true;
        set => AutoExtractCheckBox.IsChecked = value;
    }

    public MainWindow()
    {
        _workspace = new ArchiveWorkspace(_manualExtractionService);
        InitializeComponent();
    }

    private async void OpenArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "압축 파일 열기",
            Filter = "압축 파일|*.zip;*.7z;*.rar;*.tar;*.gz;*.tgz;*.tar.gz|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await OpenArchiveAsync(dialog.FileName);
    }

    private async void CreateFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "압축할 파일 선택",
            Filter = "모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await CreateArchiveAsync(dialog.FileNames);
        }
    }

    private async void CreateFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "압축할 폴더를 선택하세요.",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            await CreateArchiveAsync(new[] { dialog.SelectedPath });
        }
    }

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "압축을 풀 폴더를 선택하세요.",
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var progress = new Progress<ExtractionProgress>(value =>
        {
            OperationProgress.Value = Math.Max(0, Math.Min(100, value.Percentage));
            StatusText.Text = $"해제 중: {value.CurrentEntry} ({value.CompletedEntries:N0}/{value.TotalEntries:N0})";
        });

        await RunOperationAsync(async cancellationToken =>
        {
            await _workspace.ExtractAsync(dialog.SelectedPath, progress, cancellationToken);
            OperationProgress.Value = 100;
            StatusText.Text = "압축 해제가 완료되었습니다.";
            System.Windows.MessageBox.Show(this, "압축 해제가 완료되었습니다.", "sZIP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        _operationCancellation?.Cancel();

    private void Window_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
        {
            return;
        }

        if (paths.Length == 1
            && File.Exists(paths[0])
            && _manualExtractionService.Supports(paths[0]))
        {
            await OpenArchiveAsync(paths[0]);
            return;
        }

        await CreateArchiveAsync(paths);
    }

    private async Task OpenArchiveAsync(string archivePath)
    {
        await RunOperationAsync(async cancellationToken =>
        {
            StatusText.Text = "압축 파일을 읽는 중...";
            IReadOnlyList<ArchiveEntryInfo> entries;
            try
            {
                entries = await _workspace.OpenAsync(archivePath, null, cancellationToken);
            }
            catch (ArchivePasswordRequiredException)
            {
                var passwordDialog = new PasswordDialog { Owner = this };
                if (passwordDialog.ShowDialog() != true)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                entries = await _workspace.OpenAsync(
                    archivePath,
                    passwordDialog.Password,
                    cancellationToken);
            }
            EntriesGrid.ItemsSource = entries;
            ArchivePathText.Text = archivePath;
            ExtractButton.IsEnabled = true;
            StatusText.Text = $"{entries.Count:N0}개 항목";
        });
    }

    private async Task CreateArchiveAsync(IReadOnlyCollection<string> sourcePaths)
    {
        var initialDirectory = GetInitialDirectory(sourcePaths.First());
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "압축 파일 저장",
            Filter = "ZIP 압축 파일 (*.zip)|*.zip|7Z 압축 파일 (*.7z)|*.7z",
            DefaultExt = ".zip",
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = initialDirectory,
            FileName = GetDefaultArchiveName(sourcePaths)
        };

        if (saveDialog.ShowDialog(this) != true)
        {
            return;
        }

        var progress = new Progress<CompressionProgress>(value =>
        {
            OperationProgress.Value = Math.Max(0, Math.Min(100, value.Percentage));
            StatusText.Text = $"압축 중: {value.CurrentEntry} ({value.CompletedEntries:N0}/{value.TotalEntries:N0})";
        });

        var completed = false;
        await RunOperationAsync(async cancellationToken =>
        {
            if (string.Equals(Path.GetExtension(saveDialog.FileName), ".7z", StringComparison.OrdinalIgnoreCase))
            {
                await _sevenZipArchiveService.CreateAsync(
                    saveDialog.FileName,
                    sourcePaths,
                    progress,
                    cancellationToken);
            }
            else
            {
                await _manualArchiveService.CreateAsync(
                    saveDialog.FileName,
                    sourcePaths,
                    progress,
                    cancellationToken);
            }
            completed = true;
            OperationProgress.Value = 100;
            StatusText.Text = $"압축 생성 완료: {saveDialog.FileName}";
        });

        if (completed)
        {
            await OpenArchiveAsync(saveDialog.FileName);
        }
    }

    private void AutoExtractCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_automaticWatcher is not null)
        {
            return;
        }

        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        try
        {
            _automaticWatcher = new RecursiveArchiveWatcher(new ArchiveWatchOptions(
                downloadsPath,
                supportedExtensions: _automaticArchiveService.SupportedExtensions,
                requireZipSignature: false));
            _automaticWatcher.ArchiveReady += AutomaticWatcher_ArchiveReady;
            _automaticWatcher.Start();
            StatusText.Text = $"자동 해제 감시 중: {downloadsPath}";
            AutoExtractEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            AutoExtractCheckBox.IsChecked = false;
            ShowError("다운로드 폴더 감시를 시작하지 못했습니다.", exception.Message);
        }
    }

    private void AutoExtractCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_automaticWatcher is null)
        {
            AutoExtractEnabledChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _automaticWatcher.ArchiveReady -= AutomaticWatcher_ArchiveReady;
        _automaticWatcher.Dispose();
        _automaticWatcher = null;
        StatusText.Text = "자동 해제를 중지했습니다.";
        AutoExtractEnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AutomaticWatcher_ArchiveReady(object? sender, string archivePath) =>
        _ = ExtractAutomaticallyAsync(archivePath);

    private async Task ExtractAutomaticallyAsync(string archivePath)
    {
        var lockTaken = false;
        string? temporaryPath = null;
        try
        {
            await _automaticExtractionLock.WaitAsync(_shutdownCancellation.Token);
            lockTaken = true;
            var archiveDirectory = Path.GetDirectoryName(archivePath)!;
            temporaryPath = Path.Combine(
                archiveDirectory,
                $".szip-work-{Guid.NewGuid():N}");
            using var temporaryExclusion = _automaticWatcher?.ExcludePath(temporaryPath);

            await Dispatcher.InvokeAsync(() =>
                StatusText.Text = $"자동 해제 중: {Path.GetFileName(archivePath)}");
            await _automaticArchiveService.ExtractAsync(
                archivePath,
                temporaryPath,
                cancellationToken: _shutdownCancellation.Token);

            var outputPath = GetUniqueDirectoryPath(
                Path.Combine(archiveDirectory, GetArchiveBaseName(archivePath)));
            using var outputExclusion = _automaticWatcher?.ExcludePath(outputPath);
            Directory.Move(temporaryPath, outputPath);
            temporaryPath = null;
            await Dispatcher.InvokeAsync(() =>
                StatusText.Text = $"자동 해제 완료: {outputPath}");
            DiagnosticLog.Write("automatic-extraction.completed");
        }
        catch (ArchivePasswordRequiredException)
        {
            await Dispatcher.InvokeAsync(() =>
                StatusText.Text = $"자동 해제 건너뜀(암호 필요): {Path.GetFileName(archivePath)}");
            DiagnosticLog.Write("automatic-extraction.password-required");
        }
        catch (OperationCanceledException)
        {
            DiagnosticLog.Write("automatic-extraction.cancelled");
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() =>
                StatusText.Text = $"자동 해제 실패: {Path.GetFileName(archivePath)}");
            DiagnosticLog.Write("automatic-extraction.failed", exception);
        }
        finally
        {
            if (temporaryPath is not null && Directory.Exists(temporaryPath))
            {
                try
                {
                    Directory.Delete(temporaryPath, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (lockTaken)
            {
                _automaticExtractionLock.Release();
            }
        }
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true);

        try
        {
            await operation(_operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "작업을 취소했습니다.";
        }
        catch (ArchiveSecurityException exception)
        {
            ShowError("안전을 위해 작업을 중단했습니다.", exception.Message);
        }
        catch (InvalidDataException exception)
        {
            ShowError("올바른 압축 파일이 아니거나 파일이 손상되었습니다.", exception.Message);
        }
        catch (Exception exception)
        {
            ShowError("작업을 완료하지 못했습니다.", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        OpenArchiveButton.IsEnabled = !isBusy;
        CreateFilesButton.IsEnabled = !isBusy;
        CreateFolderButton.IsEnabled = !isBusy;
        ExtractButton.IsEnabled = !isBusy && _workspace.CurrentArchivePath is not null;
        CancelButton.IsEnabled = isBusy;
        if (isBusy)
        {
            OperationProgress.Value = 0;
        }
    }

    private void ShowError(string summary, string detail)
    {
        StatusText.Text = summary;
        System.Windows.MessageBox.Show(this, $"{summary}\n\n{detail}", "sZIP",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnClosed(EventArgs e)
    {
        _shutdownCancellation.Cancel();
        _operationCancellation?.Cancel();
        if (_automaticWatcher is not null)
        {
            _automaticWatcher.ArchiveReady -= AutomaticWatcher_ArchiveReady;
            _automaticWatcher.Dispose();
        }

        _operationCancellation?.Dispose();
        _shutdownCancellation.Dispose();
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnClosing(e);
    }

    public void AllowExit() => _allowExit = true;

    public async Task HandleCommandLineAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return;
        }

        var option = arguments[0];
        var paths = option.StartsWith("--", StringComparison.Ordinal)
            ? arguments.Skip(1).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray()
            : arguments.Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        if (string.Equals(option, "--compress", StringComparison.OrdinalIgnoreCase))
        {
            await CreateArchiveAsync(paths);
            return;
        }

        if (string.Equals(option, "--extract", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractArchivesFromShellAsync(paths);
            return;
        }

        if (paths.Length == 1 && File.Exists(paths[0]) && _manualExtractionService.Supports(paths[0]))
        {
            await OpenArchiveAsync(paths[0]);
        }
    }

    private async Task ExtractArchivesFromShellAsync(IReadOnlyCollection<string> paths)
    {
        var archives = paths
            .Where(path => File.Exists(path) && _manualExtractionService.Supports(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (archives.Length == 0)
        {
            return;
        }

        await RunOperationAsync(async cancellationToken =>
        {
            for (var index = 0; index < archives.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var archivePath = archives[index];
                var archiveDirectory = Path.GetDirectoryName(archivePath)!;
                var temporaryPath = Path.Combine(
                    archiveDirectory,
                    $".szip-shell-{Guid.NewGuid():N}");
                using var temporaryExclusion = _automaticWatcher?.ExcludePath(temporaryPath);

                try
                {
                    StatusText.Text = $"탐색기 압축 해제 중: {Path.GetFileName(archivePath)} ({index + 1}/{archives.Length})";
                    try
                    {
                        await _manualExtractionService.ExtractAsync(
                            archivePath,
                            temporaryPath,
                            cancellationToken: cancellationToken);
                    }
                    catch (ArchivePasswordRequiredException)
                    {
                        var passwordDialog = new PasswordDialog { Owner = this };
                        if (passwordDialog.ShowDialog() != true)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        await _manualExtractionService.ExtractAsync(
                            archivePath,
                            temporaryPath,
                            passwordDialog.Password,
                            cancellationToken: cancellationToken);
                    }

                    var outputPath = GetUniqueDirectoryPath(
                        Path.Combine(archiveDirectory, GetArchiveBaseName(archivePath)));
                    using var outputExclusion = _automaticWatcher?.ExcludePath(outputPath);
                    Directory.Move(temporaryPath, outputPath);
                }
                finally
                {
                    if (Directory.Exists(temporaryPath))
                    {
                        try
                        {
                            Directory.Delete(temporaryPath, recursive: true);
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }
                }
            }

            OperationProgress.Value = 100;
            StatusText.Text = $"탐색기 압축 해제 완료: {archives.Length:N0}개";
        });
    }

    private static string GetInitialDirectory(string sourcePath)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        return Directory.Exists(fullPath)
            ? Path.GetDirectoryName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                ?? fullPath
            : Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
    }

    private static string GetDefaultArchiveName(IReadOnlyCollection<string> sourcePaths)
    {
        if (sourcePaths.Count != 1)
        {
            return "새 압축 파일.zip";
        }

        var path = sourcePaths.First().TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name = Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "새 압축 파일.zip" : name + ".zip";
    }

    private static string GetArchiveBaseName(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath);
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Substring(0, fileName.Length - ".tar.gz".Length);
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string GetUniqueDirectoryPath(string desiredPath)
    {
        if (!Directory.Exists(desiredPath) && !File.Exists(desiredPath))
        {
            return desiredPath;
        }

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = $"{desiredPath} ({index})";
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("자동 해제할 폴더 이름을 만들 수 없습니다.");
    }
}
