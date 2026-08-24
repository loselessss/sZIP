using System.IO;
using System.Text;

namespace sZIP.App;

internal enum AutomaticExtractionAuditStatus
{
    Completed,
    Skipped,
    Failed,
    Cancelled
}

internal static class AutomaticExtractionAudit
{
    public static string AuditPath => Path.Combine(DiagnosticLog.LogDirectory, "automatic-extraction-audit.tsv");

    public static void Write(
        AutomaticExtractionAuditStatus status,
        string archivePath,
        string? outputPath = null,
        string? detail = null,
        bool sourceDeleted = false)
    {
        try
        {
            Directory.CreateDirectory(DiagnosticLog.LogDirectory);
            var line = string.Join("\t", new[]
            {
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
                status.ToString(),
                archivePath,
                outputPath ?? string.Empty,
                sourceDeleted ? "deleted" : "kept",
                detail ?? string.Empty
            }.Select(Escape));
            File.AppendAllText(AuditPath, line + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
        }
    }

    public static IReadOnlyList<AutomaticExtractionAuditEntry> ReadRecent(int maxEntries = 500)
    {
        if (!File.Exists(AuditPath))
        {
            return Array.Empty<AutomaticExtractionAuditEntry>();
        }

        try
        {
            return File.ReadLines(AuditPath, Encoding.UTF8)
                .Reverse()
                .Take(maxEntries)
                .Select(Parse)
                .Where(entry => entry is not null)
                .Cast<AutomaticExtractionAuditEntry>()
                .ToArray();
        }
        catch
        {
            return Array.Empty<AutomaticExtractionAuditEntry>();
        }
    }

    private static AutomaticExtractionAuditEntry? Parse(string line)
    {
        var parts = line.Split('\t');
        if (parts.Length < 6)
        {
            return null;
        }

        return new AutomaticExtractionAuditEntry(
            parts[0],
            parts[1],
            parts[2],
            parts[3],
            parts[4],
            parts[5]);
    }

    private static string Escape(string value) =>
        value.Replace('\t', ' ').Replace("\r", " ").Replace("\n", " ");
}

internal sealed class AutomaticExtractionAuditEntry
{
    public AutomaticExtractionAuditEntry(
        string time,
        string status,
        string archivePath,
        string outputPath,
        string sourceArchive,
        string detail)
    {
        Time = time;
        Status = status;
        ArchivePath = archivePath;
        OutputPath = outputPath;
        SourceArchive = sourceArchive;
        Detail = detail;
    }

    public string Time { get; }
    public string Status { get; }
    public string ArchivePath { get; }
    public string OutputPath { get; }
    public string SourceArchive { get; }
    public string Detail { get; }
}
