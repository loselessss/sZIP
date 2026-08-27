using System.Collections;
using System.Globalization;
using System.Resources;
using sZIP.Application;

namespace sZIP.App;

internal static class Localization
{
    private static readonly ResourceManager Strings = new("sZIP.App.Strings", typeof(Localization).Assembly);
    private static readonly string SystemLanguage = CultureInfo.CurrentUICulture.Name;
    private static CultureInfo _culture = CultureInfo.GetCultureInfo("en");

    public static string Language => _culture.Name;
    public static event EventHandler? Changed;

    public static string T(string key) => Strings.GetString(key, _culture) ?? key;
    public static string F(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, T(key), arguments);

    public static string Error(string message)
    {
        var resources = Strings.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;
        foreach (DictionaryEntry entry in resources)
        {
            if (string.Equals(entry.Value as string, message, StringComparison.Ordinal))
            {
                return T((string)entry.Key);
            }
        }
        return message;
    }

    public static void Apply(string preference)
    {
        _culture = CultureInfo.GetCultureInfo(AppLanguage.Resolve(preference, SystemLanguage));
        var resources = Strings.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;
        foreach (DictionaryEntry entry in resources)
        {
            System.Windows.Application.Current.Resources["Text." + entry.Key] = T((string)entry.Key);
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
