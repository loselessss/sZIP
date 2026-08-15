namespace sZIP.Application;

public sealed class ShellCommandBatch
{
    private ShellCommandBatch(
        IReadOnlyList<string> compressionPaths,
        IReadOnlyList<string> directExtractionPaths,
        IReadOnlyList<string> smartExtractionPaths,
        IReadOnlyList<IReadOnlyList<string>> otherCommands)
    {
        CompressionPaths = compressionPaths;
        DirectExtractionPaths = directExtractionPaths;
        SmartExtractionPaths = smartExtractionPaths;
        OtherCommands = otherCommands;
    }

    public IReadOnlyList<string> CompressionPaths { get; }
    public IReadOnlyList<string> DirectExtractionPaths { get; }
    public IReadOnlyList<string> SmartExtractionPaths { get; }
    public IReadOnlyList<IReadOnlyList<string>> OtherCommands { get; }

    public static ShellCommandBatch Create(IEnumerable<IReadOnlyList<string>> commands)
    {
        var compressionPaths = new List<string>();
        var directExtractionPaths = new List<string>();
        var smartExtractionPaths = new List<string>();
        var seenCompressionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDirectExtractionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenSmartExtractionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var otherCommands = new List<IReadOnlyList<string>>();

        foreach (var command in commands.Where(command => command.Count > 0))
        {
            var isCompression = string.Equals(
                command[0], "--compress", StringComparison.OrdinalIgnoreCase);
            var isDirectExtraction = string.Equals(
                command[0], "--extract-direct", StringComparison.OrdinalIgnoreCase);
            var isSmartExtraction = string.Equals(
                    command[0], "--extract-smart", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command[0], "--extract", StringComparison.OrdinalIgnoreCase);
            if (!isCompression && !isDirectExtraction && !isSmartExtraction)
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
                else if (isDirectExtraction && seenDirectExtractionPaths.Add(path))
                {
                    directExtractionPaths.Add(path);
                }
                else if (isSmartExtraction && seenSmartExtractionPaths.Add(path))
                {
                    smartExtractionPaths.Add(path);
                }
            }
        }

        return new ShellCommandBatch(
            compressionPaths, directExtractionPaths, smartExtractionPaths, otherCommands);
    }
}
