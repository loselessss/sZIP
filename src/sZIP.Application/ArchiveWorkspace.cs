using sZIP.Archive;
using sZIP.Domain;

namespace sZIP.Application;

public sealed class ArchiveWorkspace
{
    private readonly IMultiFormatArchiveService _archiveService;

    public ArchiveWorkspace(IMultiFormatArchiveService archiveService)
    {
        _archiveService = archiveService;
    }

    public string? CurrentArchivePath { get; private set; }
    public string? CurrentPassword { get; private set; }

    public async Task<IReadOnlyList<ArchiveEntryInfo>> OpenAsync(
        string archivePath,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await _archiveService.ListEntriesAsync(archivePath, password, cancellationToken);
        CurrentArchivePath = archivePath;
        CurrentPassword = password;
        return entries;
    }

    public Task ExtractAsync(
        string destinationRoot,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (CurrentArchivePath is null)
        {
            throw new InvalidOperationException("Open an archive first.");
        }

        return _archiveService.ExtractAsync(
            CurrentArchivePath,
            destinationRoot,
            CurrentPassword,
            progress,
            cancellationToken);
    }
}
