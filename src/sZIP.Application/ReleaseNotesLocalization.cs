using System.Text.RegularExpressions;

namespace sZIP.Application;

public static class ReleaseNotesLocalization
{
    private static readonly Regex LanguageMarker = new(
        @"<!--\s*sZIP:lang=(?<language>[a-zA-Z]{2}(?:-[a-zA-Z0-9]+)*)\s*-->",
        RegexOptions.CultureInvariant);

    public static string Select(string? releaseNotes, string? language)
    {
        if (string.IsNullOrWhiteSpace(releaseNotes))
        {
            return string.Empty;
        }

        var matches = LanguageMarker.Matches(releaseNotes);
        if (matches.Count == 0)
        {
            return releaseNotes.Trim();
        }

        var selected = Find(matches, Normalize(language)) ?? Find(matches, "en");
        if (selected is null)
        {
            return releaseNotes.Trim();
        }

        var start = selected.Index + selected.Length;
        var next = matches.Cast<Match>().FirstOrDefault(match => match.Index > selected.Index);
        var length = (next?.Index ?? releaseNotes.Length) - start;
        return releaseNotes.Substring(start, length).Trim();
    }

    private static Match? Find(MatchCollection matches, string language) =>
        matches.Cast<Match>().FirstOrDefault(match =>
            string.Equals(Normalize(match.Groups["language"].Value), language,
                StringComparison.Ordinal));

    private static string Normalize(string? language)
    {
        var value = language?.Trim().Replace('_', '-');
        var separator = value?.IndexOf('-') ?? -1;
        if (separator >= 0)
        {
            value = value!.Substring(0, separator);
        }

        return string.Equals(value, "ko", StringComparison.OrdinalIgnoreCase) ? "ko" : "en";
    }
}
