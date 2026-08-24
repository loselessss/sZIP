namespace sZIP.Archive;

public static class ArchivePath
{
    public static string GetSafeDestinationPath(string destinationRoot, string entryName)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            throw new ArgumentException("An output folder is required.", nameof(destinationRoot));
        }

        if (string.IsNullOrWhiteSpace(entryName))
        {
            throw new ArgumentException("An archive entry path is required.", nameof(entryName));
        }

        if (entryName.IndexOf('\0') >= 0 || Path.IsPathRooted(entryName) || entryName.Contains(':'))
        {
            throw new ArchiveSecurityException($"Unsafe archive path: {entryName}");
        }

        var root = Path.GetFullPath(destinationRoot);
        var normalizedEntry = entryName
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(Path.Combine(root, normalizedEntry));
        var rootPrefix = EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArchiveSecurityException($"Archive path escapes the output folder: {entryName}");
        }

        return destination;
    }

    public static bool IsLink(int externalAttributes)
    {
        const int unixFileTypeMask = 0xF000;
        const int unixSymbolicLink = 0xA000;
        var unixMode = (externalAttributes >> 16) & unixFileTypeMask;
        var isWindowsReparsePoint =
            (externalAttributes & (int)FileAttributes.ReparsePoint) != 0;

        return unixMode == unixSymbolicLink || isWindowsReparsePoint;
    }

    private static bool EndsInDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
        || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal);
}
