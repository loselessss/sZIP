using System.IO;
using System.Text;

namespace sZIP.App;

internal static class DiagnosticLog
{
    private const long MaxBytes = 1024 * 1024;
    private const int RetainedFiles = 3;
    private static readonly object Sync = new();

    public static string LogDirectory { get; } = Path.Combine(PackageDeployment.DataDirectory, "logs");

    public static void Write(string eventName, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                var path = Path.Combine(LogDirectory, "szip.log");
                RotateIfNeeded(path);
                var exceptionName = exception is null ? string.Empty : $" | {exception.GetType().FullName}";
                File.AppendAllText(
                    path,
                    $"{DateTimeOffset.Now:O} | {eventName}{exceptionName}{Environment.NewLine}",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Logging must never interrupt archive work.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < MaxBytes)
        {
            return;
        }

        for (var index = RetainedFiles - 1; index >= 1; index--)
        {
            var source = index == 1 ? path : path + "." + (index - 1);
            var destination = path + "." + index;
            if (File.Exists(source))
            {
                File.Copy(source, destination, overwrite: true);
            }
        }

        File.Delete(path);
    }
}
