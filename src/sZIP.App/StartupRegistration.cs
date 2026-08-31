using System.Diagnostics;
using Microsoft.Win32;

namespace sZIP.App;

internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "sZIP";

    public static bool IsEnabled
    {
        get
        {
            if (PackageDeployment.IsPackaged) return false; // Controlled through Windows Startup Apps, not HKCU Run.
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (PackageDeployment.IsPackaged) throw new InvalidOperationException(Localization.T("MsixStartupManaged"));
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Could not open Windows startup settings.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Could not determine the sZIP executable path.");
        key.SetValue(ValueName, $"\"{executablePath}\" --tray", RegistryValueKind.String);
    }
}
