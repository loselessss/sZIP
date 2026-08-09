namespace sZIP.Archive;

public static class ArchivePath
{
    public static string GetSafeDestinationPath(string destinationRoot, string entryName)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            throw new ArgumentException("출력 폴더가 필요합니다.", nameof(destinationRoot));
        }

        if (string.IsNullOrWhiteSpace(entryName))
        {
            throw new ArgumentException("압축 항목 경로가 필요합니다.", nameof(entryName));
        }

        if (entryName.IndexOf('\0') >= 0 || Path.IsPathRooted(entryName) || entryName.Contains(':'))
        {
            throw new ArchiveSecurityException($"안전하지 않은 압축 경로입니다: {entryName}");
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
            throw new ArchiveSecurityException($"출력 폴더를 벗어나는 압축 경로입니다: {entryName}");
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
