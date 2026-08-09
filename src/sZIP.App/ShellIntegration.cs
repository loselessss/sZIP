using Microsoft.Win32;

namespace sZIP.App;

internal static class ShellIntegration
{
    private const string CompressFileKey = @"Software\Classes\*\shell\sZIP.compress";
    private const string CompressDirectoryKey = @"Software\Classes\Directory\shell\sZIP.compress";
    private static readonly string[] ArchiveExtensions =
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz" };

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(CompressFileKey);
            return key is not null;
        }
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        if (!enabled)
        {
            Registry.CurrentUser.DeleteSubKeyTree(CompressFileKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(CompressDirectoryKey, throwOnMissingSubKey: false);
            foreach (var extension in ArchiveExtensions)
            {
                Registry.CurrentUser.DeleteSubKeyTree(GetOpenKey(extension), throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(GetExtractKey(extension), throwOnMissingSubKey: false);
            }

            return;
        }

        CreateVerb(
            CompressFileKey,
            "sZIP으로 압축",
            BuildCommand(executablePath, "--compress"),
            "Player");
        CreateVerb(
            CompressDirectoryKey,
            "sZIP으로 압축",
            BuildCommand(executablePath, "--compress"),
            "Player");
        foreach (var extension in ArchiveExtensions)
        {
            CreateVerb(
                GetOpenKey(extension),
                "sZIP으로 열기",
                BuildCommand(executablePath, "--open"),
                "Single");
            CreateVerb(
                GetExtractKey(extension),
                "sZIP으로 압축 풀기",
                BuildCommand(executablePath, "--extract"),
                "Player");
        }
    }

    internal static string BuildCommand(string executablePath, string option) =>
        $"\"{executablePath}\" {option} \"%1\"";

    private static string GetOpenKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP.open";

    private static string GetExtractKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP.extract";

    private static void CreateVerb(
        string keyPath,
        string caption,
        string command,
        string multiSelectModel)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
        {
            key?.SetValue(null, caption);
            key?.SetValue("Icon", System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
            key?.SetValue("MultiSelectModel", multiSelectModel);
        }

        using var commandKey = Registry.CurrentUser.CreateSubKey(keyPath + @"\command");
        commandKey?.SetValue(null, command);
    }
}
