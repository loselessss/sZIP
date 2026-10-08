using sZIP.Archive;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace sZIP.Application;

// Each preview owns one isolated directory. External viewers may keep the file open.
public sealed class ArchivePreviewSession : IDisposable
{
    private static readonly string PreviewRoot = Path.Combine(Path.GetTempPath(), "sZIP", "previews");
    public static string RootDirectory => PreviewRoot;
    private bool _keepForExternalViewer;
    private bool _disposed;

    public ArchivePreviewSession()
    {
        CleanupOldSessions();
        DirectoryPath = Path.Combine(PreviewRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public async Task<string> PrepareAsync(IMultiFormatArchiveService service, string archivePath,
        string entryName, string? password, IProgress<sZIP.Domain.ExtractionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ArchivePreviewSession));
        var path = ArchivePath.GetSafeDestinationPath(DirectoryPath, entryName);
        await Task.Run(() => service.ExtractSelectedAsync(archivePath, DirectoryPath, new[] { entryName },
            password, progress, cancellationToken), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("The preview file was not extracted.", path);
        // Keep Windows' downloaded-file protections when opening with an external application.
        try
        {
            using var handle = CreateFile(path + ":Zone.Identifier", 0x40000000, 1, IntPtr.Zero, 2, 0x80, IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                using var marker = new FileStream(handle, FileAccess.Write);
                var bytes = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
                marker.Write(bytes, 0, bytes.Length);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return path;
    }

    public void KeepForExternalViewer() => _keepForExternalViewer = true;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_keepForExternalViewer) DeleteSessionDirectory(DirectoryPath);
    }

    private static void CleanupOldSessions()
    {
        try
        {
            if (!Directory.Exists(PreviewRoot)) return;
            foreach (var path in Directory.EnumerateDirectories(PreviewRoot))
            {
                if (Directory.GetCreationTimeUtc(path) < DateTime.UtcNow.AddDays(-2))
                    DeleteSessionDirectory(path);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void DeleteSessionDirectory(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(PreviewRoot),
                    StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(fullPath), "N", out _)
                || !Directory.Exists(fullPath)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0) return;
            Directory.Delete(fullPath, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
