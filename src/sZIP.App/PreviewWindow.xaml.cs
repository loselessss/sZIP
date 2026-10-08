using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using sZIP.Application;
using sZIP.Archive;
using sZIP.Domain;
using L = sZIP.App.Localization;
using DataGrid = System.Windows.Controls.DataGrid;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace sZIP.App;

public partial class PreviewWindow : Window
{
    private readonly string _archivePath;
    private readonly ArchiveEntryInfo _entry;
    private readonly string? _password;
    private readonly IReadOnlyList<ArchiveEntryInfo> _entries;
    private readonly CancellationTokenSource _cancellation = new();
    private ArchivePreviewSession? _session;
    private WindowsPreviewHost? _windowsHost;
    private MediaElement? _media;
    private string? _filePath;
    private bool _closed;
    private bool _loading;
    private static readonly HashSet<string> ImageTypes = new(
        new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".webp", ".heic" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MediaTypes = new(
        new[] { ".mp3", ".wav", ".wma", ".aac", ".m4a", ".mp4", ".wmv", ".avi", ".mov", ".mkv", ".flac", ".ogg" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TextTypes = new(
        new[] { ".txt", ".log", ".csv", ".tsv", ".json", ".xml", ".md", ".ini", ".yaml", ".yml", ".toml",
            ".cs", ".cpp", ".c", ".h", ".py", ".js", ".ts", ".html", ".htm", ".css", ".sql", ".svg",
            ".ps1", ".bat", ".cmd", ".sh", ".srt", ".vtt", ".resx", ".config" },
        StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ExecutableTypes = new(
        new[] { ".exe", ".com", ".scr", ".msi", ".msp", ".cpl", ".dll", ".bat", ".cmd", ".ps1", ".psm1",
            ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".lnk", ".url", ".reg", ".appref-ms",
            ".py", ".pyw", ".pl", ".rb", ".sh", ".bash", ".ahk", ".jar", ".chm", ".msc", ".scf",
            ".application", ".gadget", ".inf", ".sct" },
        StringComparer.OrdinalIgnoreCase);

    public PreviewWindow(string archivePath, ArchiveEntryInfo entry, string? password,
        IReadOnlyList<ArchiveEntryInfo> entries)
    {
        _archivePath = archivePath;
        _entry = entry;
        _password = password;
        _entries = entries;
        InitializeComponent();
        FileNameText.Text = entry.FullName;
        FileNameText.ToolTip = entry.FullName;
        FileInfoText.Text = entry.IsDirectory ? L.T("PreviewFolder") : L.F("PreviewFileInfo",
            entry.Length.ToString("N0"), entry.LastWriteTime.LocalDateTime.ToString("g"));
        Loaded += async (_, _) => await LoadPreviewAsync();
        Closed += (_, _) =>
        {
            _closed = true;
            _cancellation.Cancel();
            ReleaseViewer();
            if (!_loading) CleanupSession();
        };
    }

    private async Task LoadPreviewAsync()
    {
        _loading = true;
        try
        {
            if (_entry.IsDirectory)
            {
                var prefix = _entry.FullName.Replace('\\', '/').TrimEnd('/') + "/";
                var children = _entries.Where(item => item.FullName.Replace('\\', '/')
                    .StartsWith(prefix, StringComparison.Ordinal)
                    && item.FullName.Replace('\\', '/').Length > prefix.Length).ToArray();
                PreviewContent.Content = new DataGrid
                {
                    IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false,
                    ItemsSource = children,
                    Columns =
                    {
                        new DataGridTextColumn { Header = L.T("Name"), Binding = new System.Windows.Data.Binding("FullName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
                        new DataGridTextColumn { Header = L.T("Size"), Binding = new System.Windows.Data.Binding("Length") { StringFormat = "N0" }, Width = 120 }
                    }
                };
                PreviewStatusText.Text = L.F("ItemCount", children.Length);
                return;
            }
            _session = new ArchivePreviewSession();
            var progress = new Progress<ExtractionProgress>(value =>
            {
                if (_closed) return;
                PreparationProgress.IsIndeterminate = false;
                PreparationProgress.Value = value.Percentage;
            });
            _filePath = await _session.PrepareAsync(new MultiFormatArchiveService(), _archivePath,
                _entry.FullName, _password, progress, _cancellation.Token);
            if (_closed) return;
            OpenFileButton.IsEnabled = !IsExecutable(_filePath);
            await RenderFileAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!_closed)
            {
                PreviewStatusText.Text = L.T("PreviewFailed");
                PreviewContent.Content = Message(L.Error(exception.Message));
            }
        }
        finally
        {
            _loading = false;
            if (!_closed) PreparationProgress.Visibility = Visibility.Collapsed;
            else CleanupSession();
        }
    }

    private async Task RenderFileAsync()
    {
        var path = _filePath!;
        var extension = Path.GetExtension(path);
        if (ImageTypes.Contains(extension))
        {
            try
            {
                var source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.DecodePixelWidth = 1600;
                source.UriSource = new Uri(path);
                source.EndInit();
                source.Freeze();
                PreviewContent.Content = new System.Windows.Controls.Image { Source = source, Stretch = Stretch.Uniform };
                PreviewStatusText.Text = L.T("PreviewReadOnly");
                return;
            }
            catch (Exception exception) when (exception is NotSupportedException || exception is IOException
                || exception is FileFormatException || exception is ArgumentException) { }
        }
        if (TextTypes.Contains(extension))
        {
            var text = await Task.Run(() => ReadText(path), _cancellation.Token);
            if (_closed) return;
            ShowText(text);
            PreviewStatusText.Text = new FileInfo(path).Length > 1024 * 1024
                ? L.T("PreviewTextTruncated") : L.T("PreviewReadOnly");
            return;
        }
        // Never pass executable formats to their launch association during a preview.
        if (!IsExecutable(path))
        {
            _windowsHost = new WindowsPreviewHost();
            PreviewContent.Content = _windowsHost;
            UpdateLayout();
            if (_windowsHost.TryShow(path))
            {
                PreviewStatusText.Text = L.T("PreviewWindows");
                return;
            }
            _windowsHost.Dispose();
            _windowsHost = null;
            if (MediaTypes.Contains(extension))
            {
                ShowMedia(path);
                return;
            }
        }
        await ShowBinaryAsync(path);
    }

    private void ShowText(string text)
    {
        PreviewContent.Content = new TextBox
        {
            Text = text, IsReadOnly = true, FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0)
        };
    }

    private async Task ShowBinaryAsync(string path)
    {
        var text = await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            var bytes = new byte[(int)Math.Min(4096, stream.Length)];
            var count = stream.Read(bytes, 0, bytes.Length);
            var result = new StringBuilder();
            for (var offset = 0; offset < count; offset += 16)
            {
                result.Append(offset.ToString("X8")).Append("  ");
                var lineCount = Math.Min(16, count - offset);
                for (var index = 0; index < 16; index++)
                    result.Append(index < lineCount ? bytes[offset + index].ToString("X2") : "  ").Append(' ');
                result.Append(" |");
                for (var index = 0; index < lineCount; index++)
                {
                    var value = bytes[offset + index];
                    result.Append(value >= 32 && value < 127 ? (char)value : '.');
                }
                result.AppendLine("|");
            }
            return result.ToString();
        }, _cancellation.Token);
        if (_closed) return;
        ShowText(text);
        PreviewStatusText.Text = IsExecutable(path) ? L.T("PreviewExecutable") : L.T("PreviewBinary");
    }

    private void ShowMedia(string path)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _media = new MediaElement { Source = new Uri(path), LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform };
        _media.MediaFailed += async (_, _) =>
        {
            if (_closed) return;
            ReleaseViewer();
            try { await ShowBinaryAsync(path); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (IOException exception)
            {
                if (!_closed) PreviewContent.Content = Message(L.Error(exception.Message));
            }
        };
        grid.Children.Add(_media);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var action in new[] { "PreviewPlay", "PreviewPause", "PreviewStop" })
        {
            var button = new Button { Content = L.T(action), Margin = new Thickness(0, 0, 8, 0) };
            button.Click += (_, _) =>
            {
                if (action == "PreviewPlay") _media?.Play();
                else if (action == "PreviewPause") _media?.Pause();
                else _media?.Stop();
            };
            controls.Children.Add(button);
        }
        Grid.SetRow(controls, 1);
        grid.Children.Add(controls);
        PreviewContent.Content = grid;
        PreviewStatusText.Text = L.T("PreviewMedia");
    }

    private static string ReadText(string path)
    {
        // Bound displayed text; the archive extraction itself remains cancellable and unrestricted.
        using var stream = File.OpenRead(path);
        var bytes = new byte[(int)Math.Min(1024 * 1024, stream.Length)];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = stream.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        using var memory = new MemoryStream(bytes, 0, count);
        try
        {
            using var reader = new StreamReader(memory, new UTF8Encoding(false, true), true);
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(949).GetString(bytes, 0, count);
        }
    }

    private static TextBlock Message(string text) => new()
    { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14) };

    private static bool IsExecutable(string path) =>
        ExecutableTypes.Contains(Path.GetExtension(path)) || AssocIsDangerous(Path.GetExtension(path));

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssocIsDangerous(string association);

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_filePath is null || IsExecutable(_filePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_filePath) { UseShellExecute = true });
            _session?.KeepForExternalViewer();
            PreviewStatusText.Text = L.T("PreviewExternal");
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(this, L.Error(exception.Message), L.T("PreviewOpenApp"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ReleaseViewer()
    {
        var media = _media;
        _media = null;
        media?.Close();
        _windowsHost?.Dispose();
        _windowsHost = null;
        PreviewContent.Content = null;
    }

    private void CleanupSession()
    {
        _session?.Dispose();
        _session = null;
        _cancellation.Dispose();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
