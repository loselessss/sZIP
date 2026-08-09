using sZIP.Domain;

namespace sZIP.Archive;

public interface IZipArchiveService
{
    Task<IReadOnlyList<ArchiveEntryInfo>> ListEntriesAsync(
        string archivePath,
        CancellationToken cancellationToken = default);

    Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task CreateAsync(
        string archivePath,
        IReadOnlyCollection<string> sourcePaths,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
