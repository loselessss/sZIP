using sZIP.Domain;

namespace sZIP.Archive;

public interface IMultiFormatArchiveService
{
    IReadOnlyCollection<string> SupportedExtensions { get; }

    bool Supports(string archivePath);

    Task<IReadOnlyList<ArchiveEntryInfo>> ListEntriesAsync(
        string archivePath,
        string? password = null,
        CancellationToken cancellationToken = default);

    Task ExtractAsync(
        string archivePath,
        string destinationRoot,
        string? password = null,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
