using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;
using sZIP.Domain;

namespace sZIP.Archive;

public sealed class SevenZipArchiveService
{
    private const int BufferSize = 128 * 1024;

    public async Task CreateAsync(
        string archivePath,
        IReadOnlyCollection<string> sourcePaths,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("저장할 7Z 파일 경로가 필요합니다.", nameof(archivePath));
        }

        if (sourcePaths is null || sourcePaths.Count == 0)
        {
            throw new ArgumentException("압축할 파일이나 폴더를 선택해 주세요.", nameof(sourcePaths));
        }

        var outputPath = Path.GetFullPath(archivePath);
        var items = await Task.Run(
            () => CollectSourceItems(sourcePaths, outputPath, cancellationToken),
            cancellationToken);
        var parent = Path.GetDirectoryName(outputPath)
            ?? throw new IOException("7Z 파일을 저장할 폴더를 확인할 수 없습니다.");
        Directory.CreateDirectory(parent);
        var temporaryPath = Path.Combine(
            parent,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.szip-tmp");

        try
        {
            await Task.Run(() =>
            {
                using var file = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize);
                using var writer = WriterFactory.OpenWriter(
                    file,
                    ArchiveType.SevenZip,
                    new SevenZipWriterOptions(CompressionType.LZMA2) { CompressHeader = true });
                long processedBytes = 0;
                var completedEntries = 0;
                var totalBytes = items.Sum(item => item.Length);

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item.IsDirectory)
                    {
                        writer.WriteDirectory(item.EntryName, File.GetLastWriteTime(item.SourcePath));
                    }
                    else
                    {
                        using var source = new FileStream(
                            item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                        writer.Write(item.EntryName, source, File.GetLastWriteTime(item.SourcePath));
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
            }, cancellationToken);

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
                if (!string.Equals(fullPath, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new SourceItem(
                        fullPath,
                        MakeUniqueEntryName(Path.GetFileName(fullPath), usedRoots),
                        false,
                        new FileInfo(fullPath).Length));
                }
            }
            else if (Directory.Exists(fullPath))
            {
                EnsureNotLink(fullPath);
                var root = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                CollectDirectoryItems(
                    root,
                    MakeUniqueEntryName(Path.GetFileName(root), usedRoots),
                    outputPath,
                    items,
                    cancellationToken);
            }
            else
            {
                throw new FileNotFoundException("압축할 파일이나 폴더를 찾을 수 없습니다.", sourcePath);
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("압축할 항목이 없습니다.");
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
            var childEntryName = NormalizeEntryName(entryName + "/" + Path.GetFileName(fullChildPath));
            if (Directory.Exists(fullChildPath))
            {
                CollectDirectoryItems(
                    fullChildPath, childEntryName, outputPath, items, cancellationToken);
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
            throw new ArchiveSecurityException($"링크 항목은 압축할 수 없습니다: {path}");
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

        throw new IOException("압축 항목 이름을 만들 수 없습니다.");
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
