using System.IO;
using System.ComponentModel;
using System.Diagnostics;
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
    private Stopwatch? _operationStopwatch;
    private bool _allowExit;

    public event EventHandler? AutoExtractEnabledChanged;
    public event EventHandler? HiddenToTray;
    public event EventHandler? UpdateCheckRequested;

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

    private async void ExtractDirectButton_Click(object sender, RoutedEventArgs e)
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
            UpdateExtractionProgress(value, "압축을 풀고 있습니다"));

        await RunOperationAsync(async cancellationToken =>
        {
            var archivePath = _workspace.CurrentArchivePath
                ?? throw new InvalidOperationException("먼저 압축 파일을 열어 주세요.");
            var outputPath = await ExtractCurrentArchiveAsync(
                archivePath, dialog.SelectedPath, smart: false, progress, cancellationToken);
            SetOperationCompleted("압축 해제가 완료되었습니다", outputPath);
            System.Windows.MessageBox.Show(this, "압축 해제가 완료되었습니다.", "sZIP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async void ExtractSmartButton_Click(object sender, RoutedEventArgs e)
    {
        var archivePath = _workspace.CurrentArchivePath;
        if (archivePath is null)
        {
            return;
        }

        var progress = new Progress<ExtractionProgress>(value =>
            UpdateExtractionProgress(value, "알아서 풀고 있습니다"));
        await RunOperationAsync(async cancellationToken =>
        {
            var outputPath = await ExtractCurrentArchiveAsync(
                archivePath, Path.GetDirectoryName(archivePath)!, smart: true,
                progress, cancellationToken);
            SetOperationCompleted("알아서 풀기가 완료되었습니다", outputPath);
        });
    }

    private async void ExtractSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var archivePath = _workspace.CurrentArchivePath;
        if (archivePath is null)
        {
            return;
        }

        var selectedEntries = GetSelectedEntryNames();
        if (selectedEntries.Count == 0)
        {
            ShowError("풀 항목을 선택해 주세요.", "압축 파일 목록에서 파일이나 폴더를 하나 이상 선택하세요.");
            return;
        }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "선택한 항목을 풀 폴더를 선택하세요.",
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var progress = new Progress<ExtractionProgress>(value =>
            UpdateExtractionProgress(value, "선택한 항목을 풀고 있습니다"));
        await RunOperationAsync(async cancellationToken =>
        {
            var outputPath = await ExtractCurrentArchiveAsync(
                archivePath, dialog.SelectedPath, smart: false, progress, cancellationToken,
                selectedEntries);
            SetOperationCompleted($"선택한 항목 {selectedEntries.Count:N0}개를 풀었습니다", outputPath);
        });
    }

    private IReadOnlyCollection<string> GetSelectedEntryNames()
    {
        var allEntries = EntriesGrid.ItemsSource as IEnumerable<ArchiveEntryInfo>
            ?? Enumerable.Empty<ArchiveEntryInfo>();
        var selected = EntriesGrid.SelectedItems.Cast<ArchiveEntryInfo>().ToArray();
        var names = new HashSet<string>(
            selected.Select(entry => entry.FullName),
            StringComparer.Ordinal);

        foreach (var directory in selected.Where(entry => entry.IsDirectory))
        {
            var prefix = directory.FullName.Replace('\\', '/').TrimEnd('/') + "/";
            foreach (var child in allEntries.Where(entry =>
                         entry.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal)))
            {
                names.Add(child.FullName);
            }
        }

        return names.ToArray();
    }

    private void EntriesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        ExtractSelectedButton.IsEnabled = !CancelButton.IsEnabled
            && _workspace.CurrentArchivePath is not null
            && EntriesGrid.SelectedItems.Count > 0;

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        _operationCancellation?.Cancel();

    private void UpdateCheckButton_Click(object sender, RoutedEventArgs e) =>
        UpdateCheckRequested?.Invoke(this, EventArgs.Empty);

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
            ExtractDirectButton.IsEnabled = true;
            ExtractSmartButton.IsEnabled = true;
            StatusHeadingText.Text = "압축 파일을 열었습니다";
            StatusText.Text = $"{entries.Count:N0}개 항목";
            ProgressDetailsText.Text = "그냥 풀기 또는 알아서 풀기를 선택하세요.";
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
            UpdateCompressionProgress(value));

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
            SetOperationCompleted("압축 파일을 만들었습니다", saveDialog.FileName);
        });

        if (completed)
        {
            await OpenArchiveAsync(saveDialog.FileName);
        }
    }

    private async Task<string> ExtractCurrentArchiveAsync(
        string archivePath,
        string destinationDirectory,
        bool smart,
        IProgress<ExtractionProgress> progress,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? selectedEntryNames = null)
    {
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".szip-manual-{Guid.NewGuid():N}");
        using var temporaryExclusion = _automaticWatcher?.ExcludePath(temporaryPath);
        try
        {
            if (selectedEntryNames is null)
            {
                await _manualExtractionService.ExtractAsync(
                    archivePath,
                    temporaryPath,
                    _workspace.CurrentPassword,
                    progress,
                    cancellationToken);
            }
            else
            {
                await _manualExtractionService.ExtractSelectedAsync(
                    archivePath,
                    temporaryPath,
                    selectedEntryNames,
                    _workspace.CurrentPassword,
                    progress,
                    cancellationToken);
            }
            return CompleteTemporaryExtraction(
                temporaryPath, archivePath, destinationDirectory, smart);
        }
        finally
        {
            TryDeleteDirectory(temporaryPath);
        }
    }

    private static string CompleteTemporaryExtraction(
        string temporaryPath,
        string archivePath,
        string destinationDirectory,
        bool smart)
    {
        return ExtractionPlacement.Complete(
            temporaryPath, archivePath, destinationDirectory, smart);
    }

    private void UpdateExtractionProgress(ExtractionProgress value, string heading)
    {
        UpdateOperationProgress(
            heading,
            value.CurrentEntry,
            value.Percentage,
            value.ProcessedBytes,
            value.TotalBytes,
            value.CompletedEntries,
            value.TotalEntries);
    }

    private void UpdateCompressionProgress(CompressionProgress value)
    {
        UpdateOperationProgress(
            "압축 파일을 만들고 있습니다",
            value.CurrentEntry,
            value.Percentage,
            value.ProcessedBytes,
            value.TotalBytes,
            value.CompletedEntries,
            value.TotalEntries);
    }

    private void UpdateOperationProgress(
        string heading,
        string currentEntry,
        double percentage,
        long processedBytes,
        long totalBytes,
        int completedEntries,
        int totalEntries)
    {
        var boundedPercentage = Math.Max(0, Math.Min(100, percentage));
        OperationProgress.Value = boundedPercentage;
        ProgressPercentText.Text = $"{boundedPercentage:0}%";
        StatusHeadingText.Text = heading;
        StatusText.Text = currentEntry;

        var elapsedSeconds = Math.Max(_operationStopwatch?.Elapsed.TotalSeconds ?? 0, 0.001);
        var bytesPerSecond = processedBytes / elapsedSeconds;
        var sizeText = totalBytes > 0
            ? $"{FormatBytes(processedBytes)} / {FormatBytes(totalBytes)}"
            : $"{completedEntries:N0} / {totalEntries:N0}개";
        var remainingText = totalBytes > processedBytes && bytesPerSecond > 0
            ? $" · 약 {FormatDuration(TimeSpan.FromSeconds((totalBytes - processedBytes) / bytesPerSecond))} 남음"
            : string.Empty;
        ProgressDetailsText.Text = $"{sizeText} · {FormatBytes((long)bytesPerSecond)}/s{remainingText}";
    }

    private void SetOperationCompleted(string heading, string outputPath)
    {
        OperationProgress.Value = 100;
        ProgressPercentText.Text = "100%";
        StatusHeadingText.Text = heading;
        StatusText.Text = outputPath;
        ProgressDetailsText.Text = _operationStopwatch is null
            ? "완료"
            : $"{FormatDuration(_operationStopwatch.Elapsed)} 만에 완료";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes:N0} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024):0.0} MB";
        return $"{bytes / (1024d * 1024 * 1024):0.00} GB";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}시간 {duration.Minutes}분";
        if (duration.TotalMinutes >= 1) return $"{(int)duration.TotalMinutes}분 {duration.Seconds}초";
        return $"{Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds))}초";
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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
            {
                StatusHeadingText.Text = "자동으로 압축을 풀고 있습니다";
                StatusText.Text = Path.GetFileName(archivePath);
            });
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
                SetOperationCompleted("자동 해제가 완료되었습니다", outputPath));
            DiagnosticLog.Write("automatic-extraction.completed");
        }
        catch (ArchivePasswordRequiredException)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusHeadingText.Text = "자동 해제를 건너뛰었습니다";
                StatusText.Text = $"암호 필요: {Path.GetFileName(archivePath)}";
            });
            DiagnosticLog.Write("automatic-extraction.password-required");
        }
        catch (OperationCanceledException)
        {
            DiagnosticLog.Write("automatic-extraction.cancelled");
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusHeadingText.Text = "자동 해제에 실패했습니다";
                StatusText.Text = Path.GetFileName(archivePath);
            });
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
        _operationStopwatch = Stopwatch.StartNew();
        SetBusy(true);

        try
        {
            await operation(_operationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            StatusHeadingText.Text = "작업을 취소했습니다";
            StatusText.Text = "작업을 취소했습니다.";
            ProgressDetailsText.Text = "완료되지 않은 임시 결과는 정리했습니다.";
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
            _operationStopwatch?.Stop();
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        OpenArchiveButton.IsEnabled = !isBusy;
        CreateFilesButton.IsEnabled = !isBusy;
        CreateFolderButton.IsEnabled = !isBusy;
        ExtractDirectButton.IsEnabled = !isBusy && _workspace.CurrentArchivePath is not null;
        ExtractSmartButton.IsEnabled = !isBusy && _workspace.CurrentArchivePath is not null;
        ExtractSelectedButton.IsEnabled = !isBusy
            && _workspace.CurrentArchivePath is not null
            && EntriesGrid.SelectedItems.Count > 0;
        CancelButton.IsEnabled = isBusy;
        if (isBusy)
        {
            OperationProgress.Value = 0;
            ProgressPercentText.Text = "0%";
            ProgressDetailsText.Text = "작업을 준비하고 있습니다.";
        }
    }

    private void ShowError(string summary, string detail)
    {
        StatusText.Text = summary;
        StatusHeadingText.Text = summary;
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

        if (string.Equals(option, "--extract-direct", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractArchivesFromShellAsync(paths, smart: false);
            return;
        }

        if (string.Equals(option, "--extract-smart", StringComparison.OrdinalIgnoreCase)
            || string.Equals(option, "--extract", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractArchivesFromShellAsync(paths, smart: true);
            return;
        }

        if (paths.Length == 1 && File.Exists(paths[0]) && _manualExtractionService.Supports(paths[0]))
        {
            await OpenArchiveAsync(paths[0]);
        }
    }

    private async Task ExtractArchivesFromShellAsync(
        IReadOnlyCollection<string> paths,
        bool smart)
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
            string? lastOutputPath = null;
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
                    StatusHeadingText.Text = smart
                        ? "탐색기에서 알아서 풀고 있습니다"
                        : "탐색기에서 그냥 풀고 있습니다";
                    StatusText.Text = $"{Path.GetFileName(archivePath)} ({index + 1}/{archives.Length})";
                    var progress = new Progress<ExtractionProgress>(value =>
                        UpdateExtractionProgress(
                            value,
                            smart ? "알아서 풀고 있습니다" : "그냥 풀고 있습니다"));
                    try
                    {
                        await _manualExtractionService.ExtractAsync(
                            archivePath,
                            temporaryPath,
                            progress: progress,
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
                            progress,
                            cancellationToken: cancellationToken);
                    }

                    lastOutputPath = CompleteTemporaryExtraction(
                        temporaryPath, archivePath, archiveDirectory, smart);
                }
                finally
                {
                    TryDeleteDirectory(temporaryPath);
                }
            }

            SetOperationCompleted(
                $"압축 파일 {archives.Length:N0}개를 풀었습니다",
                lastOutputPath ?? string.Empty);
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
