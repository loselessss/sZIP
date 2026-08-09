namespace sZIP.Application;

public sealed class ShellCommandBatch
{
    private ShellCommandBatch(
        IReadOnlyList<string> compressionPaths,
        IReadOnlyList<string> extractionPaths,
        IReadOnlyList<IReadOnlyList<string>> otherCommands)
    {
        CompressionPaths = compressionPaths;
        ExtractionPaths = extractionPaths;
        OtherCommands = otherCommands;
    }

    public IReadOnlyList<string> CompressionPaths { get; }
    public IReadOnlyList<string> ExtractionPaths { get; }
    public IReadOnlyList<IReadOnlyList<string>> OtherCommands { get; }

    public static ShellCommandBatch Create(IEnumerable<IReadOnlyList<string>> commands)
    {
        var compressionPaths = new List<string>();
        var extractionPaths = new List<string>();
        var seenCompressionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenExtractionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var otherCommands = new List<IReadOnlyList<string>>();

        foreach (var command in commands.Where(command => command.Count > 0))
        {
            var isCompression = string.Equals(
                command[0], "--compress", StringComparison.OrdinalIgnoreCase);
            var isExtraction = string.Equals(
                command[0], "--extract", StringComparison.OrdinalIgnoreCase);
            if (!isCompression && !isExtraction)
            {
                otherCommands.Add(command.ToArray());
                continue;
            }

            foreach (var path in command.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (isCompression && seenCompressionPaths.Add(path))
                {
                    compressionPaths.Add(path);
                }
                else if (isExtraction && seenExtractionPaths.Add(path))
                {
                    extractionPaths.Add(path);
                }
            }
        }

        return new ShellCommandBatch(compressionPaths, extractionPaths, otherCommands);
    }
}
