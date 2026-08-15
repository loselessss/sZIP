namespace sZIP.Application;

public static class ExtractionPlacement
{
    public static string Complete(
        string temporaryPath,
        string archivePath,
        string destinationDirectory,
        bool smart)
    {
        var children = Directory.EnumerateFileSystemEntries(temporaryPath).ToArray();
        if (smart)
        {
            if (children.Length == 1 && Directory.Exists(children[0]))
            {
                var desired = Path.Combine(destinationDirectory, Path.GetFileName(children[0]));
                var output = GetUniquePath(desired, preserveExtension: false);
                Directory.Move(children[0], output);
                Directory.Delete(temporaryPath);
                return output;
            }

            var smartOutput = GetUniquePath(
                Path.Combine(destinationDirectory, GetArchiveBaseName(archivePath)),
                preserveExtension: false);
            Directory.Move(temporaryPath, smartOutput);
            return smartOutput;
        }

        foreach (var child in children)
        {
            var desired = Path.Combine(destinationDirectory, Path.GetFileName(child));
            var output = GetUniquePath(desired, preserveExtension: File.Exists(child));
            if (Directory.Exists(child)) Directory.Move(child, output);
            else File.Move(child, output);
        }
        Directory.Delete(temporaryPath);
        return destinationDirectory;
    }

    private static string GetArchiveBaseName(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath);
        return fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            ? fileName.Substring(0, fileName.Length - ".tar.gz".Length)
            : Path.GetFileNameWithoutExtension(fileName);
    }

    private static string GetUniquePath(string desiredPath, bool preserveExtension)
    {
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath)) return desiredPath;
        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var extension = preserveExtension ? Path.GetExtension(desiredPath) : string.Empty;
        var name = preserveExtension
            ? Path.GetFileNameWithoutExtension(desiredPath)
            : Path.GetFileName(desiredPath);
        for (var index = 1; index < int.MaxValue; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("압축 해제할 항목의 고유 이름을 만들 수 없습니다.");
    }
}
