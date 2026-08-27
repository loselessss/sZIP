using L = sZIP.App.Localization;
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
    private readonly SemaphoreSlim _automaticArchiveExtractionLock = new(1, 1);
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private CancellationTokenSource? _operationCancellation;
    private RecursiveArchiveWatcher? _automaticWatcher;
    private Stopwatch? _operationStopwatch;
    private bool _allowExit;

    public event EventHandler? AutomaticArchiveExtractionEnabledChanged;
    public event EventHandler? HiddenToTray;
    public event EventHandler? UpdateCheckRequested;
    public event EventHandler? SettingsRequested;

    public bool IsAutomaticArchiveExtractionEnabled
    {
        get => AutomaticArchiveExtractionCheckBox.IsChecked == true;
        set => AutomaticArchiveExtractionCheckBox.IsChecked = value;
    }

    public MainWindow()
    {
        _workspace = new ArchiveWorkspace(_manualExtractionService);
        InitializeComponent();
        UpdateAutomaticArchiveExtractionToggleLabel();
        Localization.Changed += Localization_Changed;
    }

    private async void OpenArchiveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.T("OpenArchive"),
            Filter = L.T("ArchiveFilter"),
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
            Title = L.T("SelectFiles"),
            Filter = L.T("AllFilesFilter"),
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
            Description = L.T("SelectCompressFolder"),
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
            Description = L.T("SelectExtractFolder"),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var progress = new Progress<ExtractionProgress>(value =>
            UpdateExtractionProgress(value, L.T("Extracting")));

        await RunOperationAsync(async cancellationToken =>
        {
            var archivePath = _workspace.CurrentArchivePath
                ?? throw new InvalidOperationException(L.T("OpenFirst"));
            var outputPath = await ExtractCurrentArchiveAsync(
                archivePath, dialog.SelectedPath, smart: false, progress, cancellationToken);
            SetOperationCompleted(L.T("ExtractionCompleted"), outputPath);
            System.Windows.MessageBox.Show(this, L.T("ExtractionCompletedMessage"), "sZIP",
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
            UpdateExtractionProgress(value, L.T("SmartExtracting")));
        var completed = false;
        await RunOperationAsync(async cancellationToken =>
        {
            var outputPath = await ExtractCurrentArchiveAsync(
                archivePath, Path.GetDirectoryName(archivePath)!, smart: true,
                progress, cancellationToken);
            SetOperationCompleted(L.T("SmartExtractionCompleted"), outputPath);
            completed = true;
        });
        if (completed)
        {
            HideToTray();
        }
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
            ShowError(L.T("SelectItems"), L.T("SelectItemsDetail"));
            return;
        }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = L.T("SelectItemsFolder"),
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var progress = new Progress<ExtractionProgress>(value =>
            UpdateExtractionProgress(value, L.T("ExtractingSelected")));
        await RunOperationAsync(async cancellationToken =>
        {
            var outputPath = await ExtractCurrentArchiveAsync(
                archivePath, dialog.SelectedPath, smart: false, progress, cancellationToken,
                selectedEntries);
            SetOperationCompleted(L.F("SelectedCompleted", selectedEntries.Count), outputPath);
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void AuditButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AuditWindow { Owner = this };
        window.Show();
    }

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
            StatusText.Text = L.T("ReadingArchive");
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
            StatusHeadingText.Text = L.T("ArchiveOpened");
            StatusText.Text = L.F("ItemCount", entries.Count);
            ProgressDetailsText.Text = L.T("ChooseExtraction");
        });
    }

    private async Task CreateArchiveAsync(IReadOnlyCollection<string> sourcePaths)
    {
        var initialDirectory = GetInitialDirectory(sourcePaths.First());
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L.T("SaveArchive"),
            Filter = L.T("SaveArchiveFilter"),
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
            SetOperationCompleted(L.T("ArchiveCreated"), saveDialog.FileName);
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
            L.T("CreatingArchive"),
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
            : L.F("ItemProgress", completedEntries, totalEntries);
        var remainingText = totalBytes > processedBytes && bytesPerSecond > 0
            ? L.F("RemainingTime", FormatDuration(TimeSpan.FromSeconds((totalBytes - processedBytes) / bytesPerSecond)))
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
            ? L.T("Done")
            : L.F("ElapsedTime", FormatDuration(_operationStopwatch.Elapsed));
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
        if (duration.TotalHours >= 1) return L.F("HoursMinutes", (int)duration.TotalHours, duration.Minutes);
        if (duration.TotalMinutes >= 1) return L.F("MinutesSeconds", (int)duration.TotalMinutes, duration.Seconds);
        return L.F("Seconds", Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds)));
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

    private void AutomaticArchiveExtractionCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        UpdateAutomaticArchiveExtractionToggleLabel();
        if (_automaticWatcher is not null)
        {
            return;
        }

        var watchPath = GetAutomaticArchiveExtractionFolder();
        var maxArchiveBytes = GetAutomaticArchiveExtractionMaxArchiveBytes();

        try
        {
            _automaticWatcher = new RecursiveArchiveWatcher(new ArchiveWatchOptions(
                watchPath,
                maxArchiveBytes: maxArchiveBytes,
                supportedExtensions: _automaticArchiveService.SupportedExtensions,
                requireZipSignature: false));
            _automaticWatcher.ArchiveReady += AutomaticWatcher_ArchiveReady;
            _automaticWatcher.Start();
            StatusText.Text = L.F("WatchingFolder", watchPath);
            AutomaticArchiveExtractionEnabledChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            AutomaticArchiveExtractionCheckBox.IsChecked = false;
            ShowError(L.T("WatchFailed"), exception.Message);
        }
    }

    private void AutomaticArchiveExtractionCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateAutomaticArchiveExtractionToggleLabel();
        if (_automaticWatcher is null)
        {
            AutomaticArchiveExtractionEnabledChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _automaticWatcher.ArchiveReady -= AutomaticWatcher_ArchiveReady;
        _automaticWatcher.Dispose();
        _automaticWatcher = null;
        StatusText.Text = L.T("AutomaticStopped");
        AutomaticArchiveExtractionEnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AutomaticWatcher_ArchiveReady(object? sender, string archivePath) =>
        _ = ExtractAutomaticallyAsync(archivePath);

    private async Task ExtractAutomaticallyAsync(string archivePath)
    {
        var lockTaken = false;
        string? temporaryPath = null;
        try
        {
            await _automaticArchiveExtractionLock.WaitAsync(_shutdownCancellation.Token);
            lockTaken = true;
            var archiveDirectory = Path.GetDirectoryName(archivePath)!;
            temporaryPath = Path.Combine(
                archiveDirectory,
                $".szip-work-{Guid.NewGuid():N}");
            using var temporaryExclusion = _automaticWatcher?.ExcludePath(temporaryPath);

            await Dispatcher.InvokeAsync(() =>
            {
                StatusHeadingText.Text = L.T("AutomaticExtracting");
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
            var sourceDeleted = TryDeleteSourceArchiveAfterAutomaticArchiveExtraction(archivePath);
            await Dispatcher.InvokeAsync(() =>
                SetOperationCompleted(L.T("AutomaticCompleted"), outputPath));
            DiagnosticLog.Write("automatic-archive-extraction.completed");
            AutomaticArchiveExtractionAudit.Write(
                AutomaticArchiveExtractionAuditStatus.Completed,
                archivePath,
                outputPath,
                sourceDeleted ? "source archive deleted" : null,
                sourceDeleted);
        }
        catch (ArchivePasswordRequiredException)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusHeadingText.Text = L.T("AutomaticSkipped");
                StatusText.Text = L.F("PasswordRequiredFile", Path.GetFileName(archivePath));
            });
            DiagnosticLog.Write("automatic-archive-extraction.password-required");
            AutomaticArchiveExtractionAudit.Write(
                AutomaticArchiveExtractionAuditStatus.Skipped,
                archivePath,
                detail: "password required");
        }
        catch (OperationCanceledException)
        {
            DiagnosticLog.Write("automatic-archive-extraction.cancelled");
            AutomaticArchiveExtractionAudit.Write(
                AutomaticArchiveExtractionAuditStatus.Cancelled,
                archivePath);
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusHeadingText.Text = L.T("AutomaticFailed");
                StatusText.Text = Path.GetFileName(archivePath);
            });
            DiagnosticLog.Write("automatic-archive-extraction.failed", exception);
            AutomaticArchiveExtractionAudit.Write(
                AutomaticArchiveExtractionAuditStatus.Failed,
                archivePath,
                detail: exception.Message);
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
                _automaticArchiveExtractionLock.Release();
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
            StatusHeadingText.Text = L.T("OperationCanceled");
            StatusText.Text = L.T("OperationCanceledMessage");
            ProgressDetailsText.Text = L.T("TemporaryCleaned");
        }
        catch (ArchiveSecurityException exception)
        {
            ShowError(L.T("StoppedForSafety"), exception.Message);
        }
        catch (InvalidDataException exception)
        {
            ShowError(L.T("DamagedArchive"), exception.Message);
        }
        catch (Exception exception)
        {
            ShowError(L.T("OperationFailed"), exception.Message);
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
            ProgressDetailsText.Text = L.T("PreparingOperation");
        }
    }

    private void ShowError(string summary, string detail)
    {
        StatusText.Text = summary;
        StatusHeadingText.Text = summary;
        System.Windows.MessageBox.Show(this, $"{summary}\n\n{L.Error(detail)}", "sZIP",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnClosed(EventArgs e)
    {
        Localization.Changed -= Localization_Changed;
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
            HideToTray();
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

        var completed = false;
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
                        ? L.T("SmartFromExplorer")
                        : L.T("ExtractFromExplorer");
                    StatusText.Text = $"{Path.GetFileName(archivePath)} ({index + 1}/{archives.Length})";
                    var progress = new Progress<ExtractionProgress>(value =>
                        UpdateExtractionProgress(
                            value,
                            smart ? L.T("SmartExtracting") : L.T("Extracting")));
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
                archives.Length == 1 ? L.T("OneArchiveExtracted") : L.F("ArchivesExtracted", archives.Length),
                lastOutputPath ?? string.Empty);
            completed = true;
        });
        if (smart && completed)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        HiddenToTray?.Invoke(this, EventArgs.Empty);
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
            return L.T("NewArchive");
        }

        var path = sourcePaths.First().TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var name = Directory.Exists(path)
            ? Path.GetFileName(path)
            : Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? L.T("NewArchive") : name + ".zip";
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

        throw new IOException(L.T("OutputFolderError"));
    }

    public void ApplySettings(bool restartWatcher)
    {
        UpdateAutomaticArchiveExtractionToggleLabel();
        if (restartWatcher) RestartAutomaticWatcherIfEnabled();
    }

    private void Localization_Changed(object? sender, EventArgs e)
    {
        UpdateAutomaticArchiveExtractionToggleLabel();
        if (!CancelButton.IsEnabled && _automaticArchiveExtractionLock.CurrentCount > 0)
        {
            OperationProgress.Value = 0;
            ProgressPercentText.Text = "0%";
            StatusHeadingText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Text.Ready");
            ProgressDetailsText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Text.Waiting");
            if (_automaticWatcher is not null)
                StatusText.Text = L.F("WatchingFolder", GetAutomaticArchiveExtractionFolder());
            else if (_workspace.CurrentArchivePath is not null)
                StatusText.Text = _workspace.CurrentArchivePath;
            else
                StatusText.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Text.SelectArchive");
        }
    }

    private void RestartAutomaticWatcherIfEnabled()
    {
        if (AutomaticArchiveExtractionCheckBox.IsChecked != true)
        {
            return;
        }

        StopAutomaticWatcher(updateStatus: false);
        AutomaticArchiveExtractionCheckBox_Checked(this, new RoutedEventArgs());
    }

    private void StopAutomaticWatcher(bool updateStatus)
    {
        if (_automaticWatcher is null)
        {
            return;
        }

        _automaticWatcher.ArchiveReady -= AutomaticWatcher_ArchiveReady;
        _automaticWatcher.Dispose();
        _automaticWatcher = null;
        if (updateStatus)
        {
            StatusText.Text = L.T("AutomaticStopped");
        }
    }

    private string GetAutomaticArchiveExtractionFolder()
    {
        var path = UserSettings.Default.AutomaticArchiveExtractionFolder;
        return string.IsNullOrWhiteSpace(path) ? GetDefaultDownloadFolder() : path.Trim();
    }

    private int GetAutomaticArchiveExtractionMaxArchiveMb() =>
        Math.Max(1, Math.Min(10240, UserSettings.Default.AutomaticArchiveExtractionMaxArchiveMb));

    private long GetAutomaticArchiveExtractionMaxArchiveBytes() =>
        GetAutomaticArchiveExtractionMaxArchiveMb() * 1024L * 1024L;

    private static string GetDefaultDownloadFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private void UpdateAutomaticArchiveExtractionToggleLabel()
    {
        if (AutomaticArchiveExtractionCheckBox is not null)
        {
            AutomaticArchiveExtractionCheckBox.Content = AutomaticArchiveExtractionCheckBox.IsChecked == true
                ? L.T("AutomaticArchiveExtractionOn")
                : L.T("AutomaticArchiveExtractionOff");
        }
    }

    private bool TryDeleteSourceArchiveAfterAutomaticArchiveExtraction(string archivePath)
    {
        if (!UserSettings.Default.AutomaticArchiveExtractionDeleteSourceArchive)
        {
            return false;
        }

        try
        {
            File.Delete(archivePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Write("automatic-archive-extraction.source-delete.failed", exception);
            AutomaticArchiveExtractionAudit.Write(
                AutomaticArchiveExtractionAuditStatus.Failed,
                archivePath,
                detail: "source delete failed: " + exception.Message);
            return false;
        }
    }


}
