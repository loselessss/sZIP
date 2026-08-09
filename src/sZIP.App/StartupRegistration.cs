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
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Windows 시작 프로그램 설정을 열 수 없습니다.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("sZIP 실행 파일 경로를 확인할 수 없습니다.");
        key.SetValue(ValueName, $"\"{executablePath}\" --tray", RegistryValueKind.String);
    }
}
