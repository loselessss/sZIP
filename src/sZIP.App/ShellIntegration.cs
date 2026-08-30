using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace sZIP.App;

internal static class ShellIntegration
{
    private const string FileMenuKey = @"Software\Classes\*\shell\sZIP";
    private const string LegacyFileMenuKey = @"Software\Classes\*\shell\sZIP.compress";
    private const string DirectoryMenuKey = @"Software\Classes\Directory\shell\sZIP";
    private const string ArchiveProgIdKey = @"Software\Classes\sZIP.Archive";
    private const string PreferencesKey = @"Software\sZIP";
    private const string ArchiveProgId = "sZIP.Archive";
    private static readonly string[] ArchiveExtensions =
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz" };

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(FileMenuKey);
            if (key is not null)
            {
                return true;
            }

            using var legacyKey = Registry.CurrentUser.OpenSubKey(LegacyFileMenuKey);
            return legacyKey is not null;
        }
    }

    public static ShellIntegrationResult SetEnabled(bool enabled, string executablePath, bool forceModernRepair = false)
    {
        RemoveLegacyRegistration();
        if (!enabled)
        {
            return SetModernContextMenuEnabled(false);
        }

        var icon = $"{executablePath},0";
        using (var preferences = Registry.CurrentUser.CreateSubKey(PreferencesKey))
        {
            preferences?.SetValue("Language", Localization.Language);
        }
        CreateParentMenu(FileMenuKey, icon, includeExtraction: false, executablePath);
        CreateParentMenu(DirectoryMenuKey, icon, includeExtraction: false, executablePath);
        foreach (var extension in ArchiveExtensions)
        {
            CreateParentMenu(GetArchiveMenuKey(extension), icon, includeExtraction: true, executablePath);
            using var openWith = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{extension}\OpenWithProgids");
            openWith?.SetValue(ArchiveProgId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        using (var progId = Registry.CurrentUser.CreateSubKey(ArchiveProgIdKey))
        {
            progId?.SetValue(null, Localization.Language == "ko" ? "sZIP 압축 파일" : "sZIP archive file");
        }
        using (var defaultIcon = Registry.CurrentUser.CreateSubKey(ArchiveProgIdKey + @"\DefaultIcon"))
        {
            defaultIcon?.SetValue(null, icon);
        }
        using (var openCommand = Registry.CurrentUser.CreateSubKey(ArchiveProgIdKey + @"\shell\open\command"))
        {
            openCommand?.SetValue(null, BuildCommand(executablePath, "--open"));
        }

        return SetModernContextMenuEnabled(true, forceModernRepair);
    }

    internal static string BuildCommand(string executablePath, string option) =>
        $"\"{executablePath}\" {option} \"%1\"";

    private static void CreateParentMenu(
        string keyPath,
        string icon,
        bool includeExtraction,
        string executablePath)
    {
        using (var parent = Registry.CurrentUser.CreateSubKey(keyPath))
        {
            parent?.SetValue("MUIVerb", "sZIP");
            parent?.SetValue("Icon", icon);
            parent?.SetValue("MultiSelectModel", "Player");
            parent?.SetValue("SubCommands", string.Empty);
        }

        if (includeExtraction)
        {
            CreateChildVerb(keyPath, "01.smart-extract",
                Localization.Language == "ko" ? "알아서 압축 풀기" : "Smart Extract",
                BuildCommand(executablePath, "--extract-smart"));
            CreateChildVerb(keyPath, "02.extract-here",
                Localization.Language == "ko" ? "여기에 압축 풀기" : "Extract Here",
                BuildCommand(executablePath, "--extract-direct"));
            CreateChildVerb(keyPath, "03.open",
                Localization.Language == "ko" ? "sZIP으로 열기" : "Open with sZIP",
                BuildCommand(executablePath, "--open"));
        }

        CreateChildVerb(keyPath, "10.compress-zip",
            Localization.Language == "ko" ? "ZIP으로 바로 압축" : "Compress to ZIP",
            BuildCommand(executablePath, "--compress-zip"));
        CreateChildVerb(keyPath, "11.compress-7z",
            Localization.Language == "ko" ? "7Z로 바로 압축" : "Compress to 7Z",
            BuildCommand(executablePath, "--compress-7z"));
        CreateChildVerb(keyPath, "12.compress",
            Localization.Language == "ko" ? "압축 설정..." : "Compress with sZIP...",
            BuildCommand(executablePath, "--compress"));
    }

    private static void CreateChildVerb(string parentKey, string name, string caption, string command)
    {
        var childKey = parentKey + @"\shell\" + name;
        using (var key = Registry.CurrentUser.CreateSubKey(childKey))
        {
            key?.SetValue("MUIVerb", caption);
            key?.SetValue("MultiSelectModel", "Player");
        }
        using var commandKey = Registry.CurrentUser.CreateSubKey(childKey + @"\command");
        commandKey?.SetValue(null, command);
    }

    private static string GetArchiveMenuKey(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{extension}\shell\sZIP";

    private static void RemoveLegacyRegistration()
    {
        var keys = new[]
        {
            FileMenuKey,
            DirectoryMenuKey,
            LegacyFileMenuKey,
            @"Software\Classes\Directory\shell\sZIP.compress"
        };
        foreach (var key in keys)
        {
            Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }

        foreach (var extension in ArchiveExtensions)
        {
            var shellRoot = $@"Software\Classes\SystemFileAssociations\{extension}\shell";
            foreach (var verb in new[] { "sZIP", "sZIP.open", "sZIP.extract", "sZIP.extract-direct", "sZIP.extract-smart" })
            {
                Registry.CurrentUser.DeleteSubKeyTree(shellRoot + "\\" + verb, throwOnMissingSubKey: false);
            }
            using var openWith = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\{extension}\OpenWithProgids", writable: true);
            openWith?.DeleteValue(ArchiveProgId, throwOnMissingValue: false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(ArchiveProgIdKey, throwOnMissingSubKey: false);
    }

    private static bool SupportsModernMenu => Environment.OSVersion.Version >= new Version(10, 0, 22000);
    private static string PackageVersion => typeof(ShellIntegration).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
    private static string PackagePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sZIP.ContextMenu.msix");
    private static bool HasModernPayload => File.Exists(PackagePath)
        && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sZIP.ShellExtension.dll"));

    public static ShellIntegrationResult GetStatus(string executablePath)
    {
        try
        {
            var classicRegistered = new[] { FileMenuKey, DirectoryMenuKey }.All(path =>
            {
                using var key = Registry.CurrentUser.OpenSubKey(path + @"\shell\10.compress-zip\command");
                return string.Equals(key?.GetValue(null) as string, BuildCommand(executablePath, "--compress-zip"),
                    StringComparison.OrdinalIgnoreCase);
            });
            using var directoryMenu = Registry.CurrentUser.OpenSubKey(DirectoryMenuKey);
            var partialClassicRegistration = IsEnabled || directoryMenu is not null;
            if (!SupportsModernMenu)
                return new ShellIntegrationResult(classicRegistered ? "ShellStatusClassicOnly"
                    : partialClassicRegistration ? "ShellStatusRepairNeeded" : "ShellStatusDisabled",
                    classicRegistered || !partialClassicRegistration);
            var probe = RunPowerShell(ShellMenuRegistration.ProbeCommand(PackageVersion));
            if (!probe.Success)
            {
                DiagnosticLog.Write("shell-integration.status.failed " + probe.MessageKey + " " + probe.Details);
                return probe;
            }
            return ShellMenuRegistration.InterpretStatus(classicRegistered, HasModernPayload, probe.Details,
                partialClassicRegistration);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("shell-integration.status.failed", exception);
            return new ShellIntegrationResult("ShellStatusCheckFailed", false, exception.Message);
        }
    }

    internal static ShellIntegrationResult SetModernContextMenuEnabled(bool enabled, bool force = false)
    {
        if (!SupportsModernMenu && enabled)
            return new ShellIntegrationResult("ShellStatusClassicOnly", true);
        if (Environment.OSVersion.Version < new Version(10, 0, 19041))
        {
            return new ShellIntegrationResult("ShellStatusDisabled", true);
        }

        if (enabled && !HasModernPayload)
        {
            DiagnosticLog.Write("shell-integration.modern.package-missing");
            return new ShellIntegrationResult("ShellStatusPackageMissing", false);
        }

        var command = ShellMenuRegistration.RegistrationCommand(enabled, force, PackageVersion, PackagePath,
            AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var result = RunPowerShell(command);
        DiagnosticLog.Write(result.Success ? "shell-integration.modern.completed"
            : "shell-integration.modern.failed " + result.MessageKey + " " + result.Details);
        return result.Success ? new ShellIntegrationResult(enabled ? "ShellStatusReady" : "ShellStatusDisabled", true) : result;
    }

    private static ShellIntegrationResult RunPowerShell(string command)
    {
        try
        {
            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            using var process = Process.Start(new ProcessStartInfo(
                powershell,
                "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand "
                + Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + command)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (process is null) throw new InvalidOperationException("PowerShell could not be started.");
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(30000))
            {
                process.Kill();
                return new ShellIntegrationResult("ShellStatusTimedOut", false);
            }
            if (process.ExitCode != 0)
            {
                var detail = error.GetAwaiter().GetResult().Trim();
                return new ShellIntegrationResult("ShellStatusFailed", false,
                    $"Exit code: {process.ExitCode}\n" + detail.Substring(0, Math.Min(detail.Length, 4000)));
            }
            return new ShellIntegrationResult("ShellStatusReady", true, output.GetAwaiter().GetResult());
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("shell-integration.process.failed", exception);
            return new ShellIntegrationResult("ShellStatusFailed", false, exception.Message);
        }
    }
}
