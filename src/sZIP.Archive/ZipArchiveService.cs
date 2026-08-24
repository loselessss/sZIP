using System.IO.Compression;
using sZIP.Domain;

namespace sZIP.Archive;

public sealed class ZipArchiveService : IZipArchiveService
{
    private const int BufferSize = 128 * 1024;
    private readonly ExtractionPolicy _policy;

    public ZipArchiveService(ExtractionPolicy? policy = null)
    {
        _policy = policy ?? new ExtractionPolicy();
    }

    public async Task<IReadOnlyList<ArchiveEntryInfo>> ListEntriesAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ValidateArchivePath(archivePath);

        return await Task.Run<IReadOnlyList<ArchiveEntryInfo>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = OpenArchive(archivePath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            return archive.Entries.Select(entry => new ArchiveEntryInfo(
                entry.FullName,
                entry.Length,
                entry.CompressedLength,
                IsDirectory(entry),
                entry.LastWriteTime)).ToArray();
        }, cancellationToken);
    }

    public async Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateArchivePath(archivePath);
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            throw new ArgumentException("An output folder is required.", nameof(destinationRoot));
        }

        Directory.CreateDirectory(destinationRoot);
        using var stream = OpenArchive(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = archive.Entries.ToArray();

        _policy.Validate(entries);
        foreach (var entry in entries)
        {
            if (ArchivePath.IsLink(entry.ExternalAttributes))
            {
                throw new ArchiveSecurityException($"Link entries cannot be extracted: {entry.FullName}");
            }

            _ = ArchivePath.GetSafeDestinationPath(destinationRoot, entry.FullName);
        }

        var totalBytes = entries.Sum(entry => entry.Length);
        long processedBytes = 0;
        var completedEntries = 0;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = ArchivePath.GetSafeDestinationPath(destinationRoot, entry.FullName);

            if (IsDirectory(entry))
            {
                Directory.CreateDirectory(destinationPath);
            }
            else
            {
                var parent = Path.GetDirectoryName(destinationPath)
                    ?? throw new InvalidDataException("The output folder could not be determined.");
                Directory.CreateDirectory(parent);
                destinationPath = GetUniquePath(destinationPath);

                await ExtractEntryAsync(entry, destinationPath, bytesCopied =>
                {
                    progress?.Report(new ExtractionProgress(
                        entry.FullName,
                        completedEntries,
                        entries.Length,
                        processedBytes + bytesCopied,
                        totalBytes));
                }, cancellationToken);

                processedBytes += entry.Length;
                File.SetLastWriteTime(destinationPath, entry.LastWriteTime.LocalDateTime);
            }

            completedEntries++;
            progress?.Report(new ExtractionProgress(
                entry.FullName,
                completedEntries,
                entries.Length,
                processedBytes,
                totalBytes));
        }
    }

    public async Task CreateAsync(
        string archivePath,
        IReadOnlyCollection<string> sourcePaths,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("A ZIP output path is required.", nameof(archivePath));
        }

        if (sourcePaths is null || sourcePaths.Count == 0)
        {
            throw new ArgumentException("Select files or folders to archive.", nameof(sourcePaths));
        }

        var outputPath = Path.GetFullPath(archivePath);
        var items = await Task.Run(
            () => CollectSourceItems(sourcePaths, outputPath, cancellationToken),
            cancellationToken);
        var parent = Path.GetDirectoryName(outputPath)
            ?? throw new IOException("The folder for the ZIP file could not be determined.");
        Directory.CreateDirectory(parent);

        var temporaryPath = Path.Combine(
            parent,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.szip-tmp");
        var totalBytes = items.Sum(item => item.Length);
        long processedBytes = 0;
        var completedEntries = 0;

        try
        {
            using (var file = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None,
                       BufferSize,
                       FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item.IsDirectory)
                    {
                        archive.CreateEntry(item.EntryName.EndsWith("/", StringComparison.Ordinal)
                            ? item.EntryName
                            : item.EntryName + "/");
                    }
                    else
                    {
                        var entry = archive.CreateEntry(item.EntryName, CompressionLevel.Optimal);
                        entry.LastWriteTime = new DateTimeOffset(File.GetLastWriteTime(item.SourcePath));
                        await CopySourceToEntryAsync(
                            item,
                            entry,
                            copied => progress?.Report(new CompressionProgress(
                                item.EntryName,
                                completedEntries,
                                items.Count,
                                processedBytes + copied,
                                totalBytes)),
                            cancellationToken);
                        processedBytes += item.Length;
                    }

                    completedEntries++;
                    progress?.Report(new CompressionProgress(
                        item.EntryName,
                        completedEntries,
                        items.Count,
                        processedBytes,
                        totalBytes));
                }
            }

            CommitArchive(temporaryPath, outputPath);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static async Task ExtractEntryAsync(
        ZipArchiveEntry entry,
        string destinationPath,
        Action<long> reportBytes,
        CancellationToken cancellationToken)
    {
        using (var source = entry.Open())
        using (var destination = new FileStream(
                   destinationPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None,
                   BufferSize,
                   FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var buffer = new byte[BufferSize];
            long copied = 0;
            try
            {
                while (true)
                {
                    var read = await source.ReadAsync(buffer, 0, BufferSize, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer, 0, read, cancellationToken);
                    copied += read;
                    reportBytes(copied);
                }
            }
            catch
            {
                destination.Dispose();
                File.Delete(destinationPath);
                throw;
            }
        }
    }

    private static async Task CopySourceToEntryAsync(
        SourceItem item,
        ZipArchiveEntry entry,
        Action<long> reportBytes,
        CancellationToken cancellationToken)
    {
        using (var source = new FileStream(
                   item.SourcePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   BufferSize,
                   FileOptions.Asynchronous | FileOptions.SequentialScan))
        using (var destination = entry.Open())
        {
            var buffer = new byte[BufferSize];
            long copied = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer, 0, read, cancellationToken);
                copied += read;
                reportBytes(copied);
            }
        }
    }

    private static IReadOnlyList<SourceItem> CollectSourceItems(
        IReadOnlyCollection<string> sourcePaths,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var items = new List<SourceItem>();
        var usedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(sourcePath);
            if (File.Exists(fullPath))
            {
                EnsureNotLink(fullPath);
                var entryName = MakeUniqueEntryName(Path.GetFileName(fullPath), usedRoots);
                if (!string.Equals(fullPath, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new SourceItem(fullPath, entryName, false, new FileInfo(fullPath).Length));
                }
            }
            else if (Directory.Exists(fullPath))
            {
                EnsureNotLink(fullPath);
                var trimmedRoot = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var rootName = MakeUniqueEntryName(Path.GetFileName(trimmedRoot), usedRoots);
                CollectDirectoryItems(trimmedRoot, rootName, outputPath, items, cancellationToken);
            }
            else
            {
                throw new FileNotFoundException("A file or folder to archive was not found.", sourcePath);
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("There are no items to archive.");
        }

        return items;
    }

    private static void CollectDirectoryItems(
        string directoryPath,
        string entryName,
        string outputPath,
        ICollection<SourceItem> items,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        items.Add(new SourceItem(directoryPath, NormalizeEntryName(entryName), true, 0));

        foreach (var childPath in Directory.EnumerateFileSystemEntries(directoryPath)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullChildPath = Path.GetFullPath(childPath);
            if (string.Equals(fullChildPath, outputPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EnsureNotLink(fullChildPath);
            var childEntryName = NormalizeEntryName(
                entryName + "/" + Path.GetFileName(fullChildPath));
            if (Directory.Exists(fullChildPath))
            {
                CollectDirectoryItems(
                    fullChildPath,
                    childEntryName,
                    outputPath,
                    items,
                    cancellationToken);
            }
            else
            {
                items.Add(new SourceItem(
                    fullChildPath,
                    childEntryName,
                    false,
                    new FileInfo(fullChildPath).Length));
            }
        }
    }

    private static void EnsureNotLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArchiveSecurityException($"Link entries cannot be archived: {path}");
        }
    }

    private static string MakeUniqueEntryName(string desiredName, ISet<string> usedNames)
    {
        var normalized = NormalizeEntryName(desiredName);
        if (usedNames.Add(normalized))
        {
            return normalized;
        }

        var name = Path.GetFileNameWithoutExtension(normalized);
        var extension = Path.GetExtension(normalized);
        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = $"{name} ({index}){extension}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create an archive entry name.");
    }

    private static string NormalizeEntryName(string name) =>
        name.Replace('\\', '/').TrimStart('/');

    private static void CommitArchive(string temporaryPath, string outputPath)
    {
        if (!File.Exists(outputPath))
        {
            File.Move(temporaryPath, outputPath);
            return;
        }

        try
        {
            File.Replace(temporaryPath, outputPath, null);
        }
        catch (IOException)
        {
            File.Copy(temporaryPath, outputPath, overwrite: true);
            File.Delete(temporaryPath);
        }
    }

    private static FileStream OpenArchive(string archivePath) => new(
        archivePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        BufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static void ValidateArchivePath(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("An archive path is required.", nameof(archivePath));
        }
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("Archive file not found.", archivePath);
        }
    }

    private static bool IsDirectory(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith("/", StringComparison.Ordinal)
        || entry.FullName.EndsWith("\\", StringComparison.Ordinal);

    private static string GetUniquePath(string desiredPath)
    {
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath)!;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);

        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create an available output file name.");
    }

    private sealed class SourceItem
    {
        public SourceItem(string sourcePath, string entryName, bool isDirectory, long length)
        {
            SourcePath = sourcePath;
            EntryName = entryName;
            IsDirectory = isDirectory;
            Length = length;
        }

        public string SourcePath { get; }
        public string EntryName { get; }
        public bool IsDirectory { get; }
        public long Length { get; }
    }
}
