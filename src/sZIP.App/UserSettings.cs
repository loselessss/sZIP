using System.Configuration;

namespace sZIP.App;

internal sealed class UserSettings : ApplicationSettingsBase
{
    private static readonly UserSettings Instance =
        (UserSettings)Synchronized(new UserSettings());

    public static UserSettings Default => Instance;

    [UserScopedSetting]
    [DefaultSettingValue("True")]
    public bool AutomaticArchiveExtractionEnabled
    {
        get => (bool)this[nameof(AutomaticArchiveExtractionEnabled)];
        set => this[nameof(AutomaticArchiveExtractionEnabled)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string AutomaticArchiveExtractionFolder
    {
        get => (string)this[nameof(AutomaticArchiveExtractionFolder)];
        set => this[nameof(AutomaticArchiveExtractionFolder)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("200")]
    public int AutomaticArchiveExtractionMaxArchiveMb
    {
        get => (int)this[nameof(AutomaticArchiveExtractionMaxArchiveMb)];
        set => this[nameof(AutomaticArchiveExtractionMaxArchiveMb)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("False")]
    public bool AutomaticArchiveExtractionDeleteSourceArchive
    {
        get => (bool)this[nameof(AutomaticArchiveExtractionDeleteSourceArchive)];
        set => this[nameof(AutomaticArchiveExtractionDeleteSourceArchive)] = value;
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
