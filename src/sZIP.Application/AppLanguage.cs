namespace sZIP.Application;

public static class AppLanguage
{
    public static string Resolve(string? preference, string systemLanguage)
    {
        var language = string.IsNullOrWhiteSpace(preference)
            || string.Equals(preference, "system", StringComparison.OrdinalIgnoreCase)
            ? systemLanguage : preference!;
        var primary = language.Trim().Replace('_', '-').Split('-')[0];
        return string.Equals(primary, "ko", StringComparison.OrdinalIgnoreCase) ? "ko" : "en";
    }
}
