using System.Configuration;

namespace sZIP.App;

internal sealed class UserSettings : ApplicationSettingsBase
{
    private static readonly UserSettings Instance =
        (UserSettings)Synchronized(new UserSettings());

    public static UserSettings Default => Instance;

    [UserScopedSetting]
    [DefaultSettingValue("True")]
    public bool AutoExtractEnabled
    {
        get => (bool)this[nameof(AutoExtractEnabled)];
        set => this[nameof(AutoExtractEnabled)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("False")]
    public bool TrayHintShown
    {
        get => (bool)this[nameof(TrayHintShown)];
        set => this[nameof(TrayHintShown)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string LastUpdateCheckUtc
    {
        get => (string)this[nameof(LastUpdateCheckUtc)];
        set => this[nameof(LastUpdateCheckUtc)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string SkippedUpdateVersion
    {
        get => (string)this[nameof(SkippedUpdateVersion)];
        set => this[nameof(SkippedUpdateVersion)] = value;
    }
}
