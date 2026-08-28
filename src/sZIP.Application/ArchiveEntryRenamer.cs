using sZIP.Archive;
using sZIP.Domain;

namespace sZIP.Application;

public sealed class ArchiveEntryRenamer
{
    private readonly IMultiFormatArchiveService _reader;
    private readonly ZipArchiveService _zipWriter;
    private readonly SevenZipArchiveService _sevenZipWriter;

    public ArchiveEntryRenamer(
        IMultiFormatArchiveService reader,
        ZipArchiveService zipWriter,
        SevenZipArchiveService sevenZipWriter)
    {
        _reader = reader;
        _zipWriter = zipWriter;
        _sevenZipWriter = sevenZipWriter;
    }

    public bool Supports(string archivePath)
    {
        var extension = Path.GetExtension(archivePath);
        return string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RenameAsync(
        string archivePath,
        string entryName,
        string newName,
        string? password = null,
        IProgress<CompressionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(archivePath))
        {
            throw new NotSupportedException("Only ZIP and 7Z archives can be edited.");
        }

        var validatedName = ValidateNewName(newName);
        var entries = await _reader.ListEntriesAsync(archivePath, password, cancellationToken);
        if (entries.Any(entry => entry.IsEncrypted))
        {
            throw new NotSupportedException("Encrypted archives cannot be renamed without removing encryption.");
        }

        var normalizedEntry = NormalizeEntryName(entryName);
        if (!entries.Any(entry => string.Equals(
                NormalizeEntryName(entry.FullName), normalizedEntry, StringComparison.Ordinal)))
        {
            throw new FileNotFoundException("The selected archive entry was not found.");
        }

        var archiveFullPath = Path.GetFullPath(archivePath);
        var archiveDirectory = Path.GetDirectoryName(archiveFullPath)
            ?? throw new IOException("The archive folder could not be determined.");
        var extension = Path.GetExtension(archiveFullPath);
        var workPath = Path.Combine(archiveDirectory, $".szip-rename-{Guid.NewGuid():N}");
        var rebuiltPath = Path.Combine(archiveDirectory,
            $".{Path.GetFileNameWithoutExtension(archiveFullPath)}.{Guid.NewGuid():N}{extension}");

        try
        {
            await _reader.ExtractAsync(
                archiveFullPath, workPath, password, cancellationToken: cancellationToken);
            RenameExtractedEntry(workPath, normalizedEntry, validatedName);
            var sourcePaths = Directory.EnumerateFileSystemEntries(workPath).ToArray();
            if (sourcePaths.Length == 0)
            {
                throw new InvalidDataException("The archive does not contain any items to rebuild.");
            }

            if (string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase))
            {
                await _sevenZipWriter.CreateAsync(rebuiltPath, sourcePaths, progress, cancellationToken);
            }
            else
            {
                await _zipWriter.CreateAsync(rebuiltPath, sourcePaths, progress, cancellationToken);
            }

            ReplaceOriginal(rebuiltPath, archiveFullPath);
        }
        finally
        {
            TryDeleteDirectory(workPath);
            TryDeleteFile(rebuiltPath);
        }
    }

    private static string ValidateNewName(string newName)
    {
        var value = newName?.Trim() ?? string.Empty;
        if (value.Length == 0 || value is "." or ".."
            || value.EndsWith(".", StringComparison.Ordinal)
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0)
        {
            throw new ArgumentException("Enter a valid file or folder name.");
        }
        return value;
    }

    private static string NormalizeEntryName(string value) =>
        value.Replace('\\', '/').Trim('/');

    private static void RenameExtractedEntry(string root, string entryName, string newName)
    {
        var sourcePath = GetSafePath(root, entryName);
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("The extracted archive entry was not found.", entryName);
        }

        var parent = Path.GetDirectoryName(sourcePath)
            ?? throw new IOException("The archive entry folder could not be determined.");
        var destinationPath = Path.Combine(parent, newName);
        if (string.Equals(sourcePath, destinationPath, StringComparison.Ordinal))
        {
            return;
        }
        if ((!string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase)
                && (File.Exists(destinationPath) || Directory.Exists(destinationPath))))
        {
            throw new IOException("An archive entry with that name already exists in the same folder.");
        }

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            var intermediate = Path.Combine(parent, $".szip-case-{Guid.NewGuid():N}");
            MoveEntry(sourcePath, intermediate);
            try
            {
                MoveEntry(intermediate, destinationPath);
            }
            catch
            {
                MoveEntry(intermediate, sourcePath);
                throw;
            }
            return;
        }

        MoveEntry(sourcePath, destinationPath);
    }

    private static string GetSafePath(string root, string entryName)
    {
        var rootFullPath = Path.GetFullPath(root).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath,
            entryName.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The archive entry path is not safe.");
        }
        return candidate;
    }

    private static void MoveEntry(string source, string destination)
    {
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination);
    }

    private static void ReplaceOriginal(string rebuiltPath, string archivePath)
    {
        var backupPath = archivePath + ".szip-backup-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Replace(rebuiltPath, archivePath, backupPath, ignoreMetadataErrors: true);
            TryDeleteFile(backupPath);
            return;
        }
        catch (IOException)
        {
            TryDeleteFile(backupPath);
        }
        catch (PlatformNotSupportedException)
        {
        }

        File.Move(archivePath, backupPath);
        try
        {
            File.Move(rebuiltPath, archivePath);
            File.Delete(backupPath);
        }
        catch
        {
            TryDeleteFile(archivePath);
            if (File.Exists(backupPath)) File.Move(backupPath, archivePath);
            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
