using System.Security.Cryptography;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using sZIP.Domain;

namespace sZIP.Archive;

public sealed class MultiFormatArchiveService : IMultiFormatArchiveService
{
    private const int BufferSize = 128 * 1024;
    private static readonly HashSet<string> KnownExtensions = new(
        new[]
        {
            ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz"
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly ExtractionPolicy _policy;

    public MultiFormatArchiveService(ExtractionPolicy? policy = null)
    {
        _policy = policy ?? new ExtractionPolicy();
    }

    public IReadOnlyCollection<string> SupportedExtensions => KnownExtensions;

    public bool Supports(string archivePath)
    {
        var extension = GetCompoundExtension(archivePath);
        return KnownExtensions.Contains(extension);
    }

    public async Task<IReadOnlyList<ArchiveEntryInfo>> ListEntriesAsync(
        string archivePath,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        ValidateArchivePath(archivePath);

        try
        {
            return await Task.Run<IReadOnlyList<ArchiveEntryInfo>>(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var prepared = OpenArchiveSource(archivePath);
                using var archive = ArchiveFactory.OpenArchive(
                    prepared.Stream, CreateOptions(archivePath, password));
                var entries = archive.Entries.ToArray();
                EnsurePasswordProvided(entries.Any(entry => entry.IsEncrypted), password);
                ValidateEntries(entries.Select(entry => new EntryMetrics(
                    entry.Key ?? string.Empty,
                    entry.Size,
                    entry.CompressedSize,
                    entry.IsDirectory,
                    entry.IsEncrypted,
                    GetLinkTarget(entry),
                    GetAttributes(entry))));

                return entries.Select(entry => new ArchiveEntryInfo(
                    GetEntryKey(entry),
                    entry.Size,
                    entry.CompressedSize,
                    entry.IsDirectory,
                    ToDateTimeOffset(entry.LastModifiedTime),
                    entry.IsEncrypted)).ToArray();
            }, cancellationToken);
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            throw new ArchivePasswordRequiredException("압축 파일 암호가 필요하거나 올바르지 않습니다.", exception);
        }
    }

    public async Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        string? password = null,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await ExtractCoreAsync(
            archivePath, destinationRoot, null, password, progress, cancellationToken);
    }

    public async Task ExtractSelectedAsync(
        string archivePath,
        string destinationRoot,
        IReadOnlyCollection<string> selectedEntryNames,
        string? password = null,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (selectedEntryNames is null || selectedEntryNames.Count == 0)
        {
            throw new ArgumentException("풀 압축 항목을 하나 이상 선택해 주세요.", nameof(selectedEntryNames));
        }

        var selected = new HashSet<string>(
            selectedEntryNames.Select(NormalizeEntryKey),
            StringComparer.Ordinal);
        await ExtractCoreAsync(
            archivePath, destinationRoot, selected, password, progress, cancellationToken);
    }

    private async Task ExtractCoreAsync(
        string archivePath,
        string destinationRoot,
        ISet<string>? selectedEntryNames,
        string? password,
        IProgress<ExtractionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateArchivePath(archivePath);
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            throw new ArgumentException("출력 폴더가 필요합니다.", nameof(destinationRoot));
        }

        try
        {
            EntryMetrics[] metrics;
            using (var prepared = OpenArchiveSource(archivePath))
            using (var archive = ArchiveFactory.OpenArchive(
                       prepared.Stream, CreateOptions(archivePath, password)))
            {
                metrics = archive.Entries.Select(entry => new EntryMetrics(
                    GetEntryKey(entry),
                    entry.Size,
                    entry.CompressedSize,
                    entry.IsDirectory,
                    entry.IsEncrypted,
                    GetLinkTarget(entry),
                    GetAttributes(entry))).ToArray();
                EnsurePasswordProvided(metrics.Any(entry => entry.IsEncrypted), password);
            }

            ValidateEntries(metrics);
            var selectedMetrics = selectedEntryNames is null
                ? metrics
                : metrics.Where(entry => selectedEntryNames.Contains(NormalizeEntryKey(entry.Key))).ToArray();
            if (selectedEntryNames is not null && selectedMetrics.Length == 0)
            {
                throw new InvalidDataException("선택한 항목을 압축 파일에서 찾을 수 없습니다.");
            }
            Directory.CreateDirectory(destinationRoot);
            var totalBytes = selectedMetrics.Sum(entry => entry.Size);
            long processedBytes = 0;
            var completedEntries = 0;

            using (var prepared = OpenArchiveSource(archivePath))
            using (var archive = ArchiveFactory.OpenArchive(
                       prepared.Stream, CreateOptions(archivePath, password)))
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entryName = GetEntryKey(entry);
                    if (selectedEntryNames is not null
                        && !selectedEntryNames.Contains(NormalizeEntryKey(entryName)))
                    {
                        continue;
                    }
                    var destinationPath = ArchivePath.GetSafeDestinationPath(destinationRoot, entryName);
                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(destinationPath);
                    }
                    else
                    {
                        var parent = Path.GetDirectoryName(destinationPath)
                            ?? throw new InvalidDataException("출력 폴더를 확인할 수 없습니다.");
                        Directory.CreateDirectory(parent);
                        destinationPath = GetUniquePath(destinationPath);

                        using (var source = entry.OpenEntryStream())
                        {
                            await CopyEntryAsync(
                                source,
                                destinationPath,
                                copied => progress?.Report(new ExtractionProgress(
                                    entryName,
                                    completedEntries,
                                    selectedMetrics.Length,
                                    processedBytes + copied,
                                    totalBytes)),
                                cancellationToken);
                        }

                        processedBytes += entry.Size;
                        if (entry.LastModifiedTime.HasValue)
                        {
                            File.SetLastWriteTime(destinationPath, entry.LastModifiedTime.Value);
                        }
                    }

                    completedEntries++;
                    progress?.Report(new ExtractionProgress(
                        entryName,
                        completedEntries,
                        selectedMetrics.Length,
                        processedBytes,
                        totalBytes));
                }
            }
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            throw new ArchivePasswordRequiredException("압축 파일 암호가 필요하거나 올바르지 않습니다.", exception);
        }
    }

    private void ValidateEntries(IEnumerable<EntryMetrics> source)
    {
        var entries = source.ToArray();
        if (entries.Length > _policy.MaxEntryCount)
        {
            throw new ArchiveSecurityException(
                $"압축 파일의 항목 수가 허용 한도({_policy.MaxEntryCount:N0}개)를 초과합니다.");
        }

        long totalSize = 0;
        long totalCompressedSize = 0;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                throw new ArchiveSecurityException("이름이 없는 압축 항목을 발견했습니다.");
            }

            if (!string.IsNullOrWhiteSpace(entry.LinkTarget)
                || (entry.Attributes.HasValue && ArchivePath.IsLink(entry.Attributes.Value)))
            {
                throw new ArchiveSecurityException($"링크 항목은 해제할 수 없습니다: {entry.Key}");
            }

            _ = ArchivePath.GetSafeDestinationPath(Path.GetTempPath(), entry.Key);
            if (entry.Size > _policy.MaxSingleFileBytes)
            {
                throw new ArchiveSecurityException($"'{entry.Key}'의 크기가 단일 파일 한도를 초과합니다.");
            }

            try
            {
                totalSize = checked(totalSize + entry.Size);
                totalCompressedSize = checked(totalCompressedSize + entry.CompressedSize);
            }
            catch (OverflowException)
            {
                throw new ArchiveSecurityException("압축 해제 예상 크기가 올바르지 않습니다.");
            }
        }

        if (totalSize > _policy.MaxTotalBytes)
        {
            throw new ArchiveSecurityException(
                $"전체 해제 크기가 허용 한도({_policy.MaxTotalBytes:N0}바이트)를 초과합니다.");
        }

        if (totalCompressedSize > 0 && totalSize / (double)totalCompressedSize > _policy.MaxExpansionRatio)
        {
            throw new ArchiveSecurityException(
                $"압축률이 안전 한도({_policy.MaxExpansionRatio:0.#}배)를 초과합니다.");
        }
    }

    private static async Task CopyEntryAsync(
        Stream source,
        string destinationPath,
        Action<long> reportBytes,
        CancellationToken cancellationToken)
    {
        try
        {
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
        catch
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
    }

    private static ReaderOptions CreateOptions(string archivePath, string? password)
    {
        var options = new ReaderOptions
        {
            LeaveStreamOpen = false,
            Password = string.IsNullOrEmpty(password) ? null : password,
            BufferSize = BufferSize
        };
        if (string.Equals(Path.GetExtension(archivePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            options.ArchiveEncoding = new ArchiveEncoding
            {
                CustomDecoder = (data, _, _, _) => DecodeArchiveName(data)
            };
        }

        return options;
    }

    private static void EnsurePasswordProvided(bool isEncrypted, string? password)
    {
        if (isEncrypted && string.IsNullOrEmpty(password))
        {
            throw new ArchivePasswordRequiredException("압축 파일 암호가 필요합니다.");
        }
    }

    private PreparedArchiveSource OpenArchiveSource(string archivePath)
    {
        if (!IsTarGZip(archivePath))
        {
            return new PreparedArchiveSource(
                new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read),
                null);
        }

        var temporaryPath = Path.Combine(
            Path.GetTempPath(),
            "szip-" + Guid.NewGuid().ToString("N") + ".tar");
        try
        {
            using (var input = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var gzip = new System.IO.Compression.GZipStream(
                       input,
                       System.IO.Compression.CompressionMode.Decompress))
            using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[BufferSize];
                long total = 0;
                while (true)
                {
                    var read = gzip.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        break;
                    }

                    total = checked(total + read);
                    if (total > _policy.MaxTotalBytes + 64L * 1024 * 1024)
                    {
                        throw new ArchiveSecurityException("TAR 압축 해제 준비 크기가 안전 한도를 초과합니다.");
                    }

                    output.Write(buffer, 0, read);
                }
            }

            return new PreparedArchiveSource(
                new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read),
                temporaryPath);
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

    private static bool IsTarGZip(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(value.Value) : DateTimeOffset.MinValue;

    private static string NormalizeEntryKey(string value) =>
        value.Replace('\\', '/').TrimEnd('/');

    private static string GetEntryKey(IArchiveEntry entry) =>
        NormalizeArchiveName(entry.Key ?? string.Empty);

    private static string NormalizeArchiveName(string value) =>
        value.TrimEnd('\0', ' ').Normalize(NormalizationForm.FormC);

    private static string DecodeArchiveName(byte[] data)
    {
        try
        {
            return NormalizeArchiveName(new UTF8Encoding(false, true).GetString(data));
        }
        catch (DecoderFallbackException)
        {
            return NormalizeArchiveName(Encoding.GetEncoding(437).GetString(data));
        }
    }

    private static string? GetLinkTarget(IArchiveEntry entry)
    {
        try
        {
            return entry.LinkTarget;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private static int? GetAttributes(IArchiveEntry entry)
    {
        try
        {
            return entry.Attrib;
        }
        catch (NotImplementedException)
        {
            return null;
        }
    }

    private static void ValidateArchivePath(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("압축 파일 경로가 필요합니다.", nameof(archivePath));
        }

        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("압축 파일을 찾을 수 없습니다.", archivePath);
        }
    }

    private static string GetCompoundExtension(string path)
    {
        var fileName = Path.GetFileName(path);
        foreach (var compoundExtension in new[] { ".tar.gz", ".tar.bz2", ".tar.xz", ".tar.zst" })
        {
            if (fileName.EndsWith(compoundExtension, StringComparison.OrdinalIgnoreCase))
            {
                return compoundExtension switch
                {
                    ".tar.gz" => ".tgz",
                    ".tar.bz2" => ".tbz2",
                    ".tar.xz" => ".txz",
                    ".tar.zst" => ".tzst",
                    _ => compoundExtension
                };
            }
        }

        return Path.GetExtension(fileName);
    }

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

        throw new IOException("사용 가능한 출력 파일 이름을 만들 수 없습니다.");
    }

    private sealed class EntryMetrics
    {
        public EntryMetrics(
            string key,
            long size,
            long compressedSize,
            bool isDirectory,
            bool isEncrypted,
            string? linkTarget,
            int? attributes)
        {
            Key = key;
            Size = size;
            CompressedSize = compressedSize;
            IsDirectory = isDirectory;
            IsEncrypted = isEncrypted;
            LinkTarget = linkTarget;
            Attributes = attributes;
        }

        public string Key { get; }
        public long Size { get; }
        public long CompressedSize { get; }
        public bool IsDirectory { get; }
        public bool IsEncrypted { get; }
        public string? LinkTarget { get; }
        public int? Attributes { get; }
    }

    private sealed class PreparedArchiveSource : IDisposable
    {
        private readonly string? _temporaryPath;

        public PreparedArchiveSource(Stream stream, string? temporaryPath)
        {
            Stream = stream;
            _temporaryPath = temporaryPath;
        }

        public Stream Stream { get; }

        public void Dispose()
        {
            Stream.Dispose();
            if (_temporaryPath is not null && File.Exists(_temporaryPath))
            {
                File.Delete(_temporaryPath);
            }
        }
    }
}
