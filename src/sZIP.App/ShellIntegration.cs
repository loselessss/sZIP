using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

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
                Registry.CurrentUser.DeleteSubKeyTree(GetDirectExtractKey(extension), throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(GetSmartExtractKey(extension), throwOnMissingSubKey: false);
            }

            SetModernContextMenuEnabled(false);

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
            Registry.CurrentUser.DeleteSubKeyTree(GetLegacyExtractKey(extension), throwOnMissingSubKey: false);
            CreateVerb(
                GetOpenKey(extension),
                "sZIP으로 열기",
                BuildCommand(executablePath, "--open"),
                "Single");
            CreateVerb(
                GetDirectExtractKey(extension),
                "sZIP 그냥 풀기",
                BuildCommand(executablePath, "--extract-direct"),
                "Player");
            CreateVerb(
                GetSmartExtractKey(extension),
                "sZIP 알아서 풀기",
                BuildCommand(executablePath, "--extract-smart"),
                "Player");
        }
        SetModernContextMenuEnabled(true);
    }

    internal static string BuildCommand(string executablePath, string option) =>
        $"\"{executablePath}\" {option} \"%1\"";

    private static string GetOpenKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP.open";

    private static string GetDirectExtractKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP.extract-direct";

    private static string GetSmartExtractKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP.extract-smart";

    private static string GetLegacyExtractKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP.extract";

    internal static void SetModernContextMenuEnabled(bool enabled)
    {
        if (Environment.OSVersion.Version < new Version(10, 0, 19041))
        {
            return;
        }

        var packagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sZIP.ContextMenu.msix");
        if (enabled && !File.Exists(packagePath))
        {
            return;
        }

        var packageVersion = typeof(ShellIntegration).Assembly.GetName().Version?.ToString()
            ?? "1.0.0.0";
        var escapedPackage = EscapePowerShellLiteral(packagePath);
        var escapedLocation = EscapePowerShellLiteral(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var command = enabled
            ? "$p=Get-AppxPackage -Name 'sZIP.ContextMenu'; "
              + $"if(-not $p -or $p.Version.ToString() -ne '{packageVersion}'){{ "
              + "if($p){$p | Remove-AppxPackage}; "
              + $"Add-AppxPackage -Path '{escapedPackage}' -ExternalLocation '{escapedLocation}' -AllowUnsigned }}"
            : "$p=Get-AppxPackage -Name 'sZIP.ContextMenu'; if($p){$p | Remove-AppxPackage}";

        try
        {
            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            using var process = Process.Start(new ProcessStartInfo(
                powershell,
                "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -Command \""
                + command.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit(30000);
            if (process is not null && process.ExitCode != 0)
            {
                DiagnosticLog.Write($"shell-integration.modern.failed exit={process.ExitCode}");
            }
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("shell-integration.modern.failed", exception);
        }
    }

    private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''");

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
