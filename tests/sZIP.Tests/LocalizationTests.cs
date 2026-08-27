using System.Text.RegularExpressions;
using System.Xml.Linq;
using sZIP.Application;

namespace sZIP.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("system", "ko-KR", "ko")]
    [InlineData("system", "en-US", "en")]
    [InlineData("system", "fr-FR", "en")]
    [InlineData("ko", "en-US", "ko")]
    [InlineData("en", "ko-KR", "en")]
    [InlineData("ko_KR", "en-US", "ko")]
    [InlineData(null, "ko-KR", "ko")]
    public void LanguagePreferenceOverridesSystemLanguage(string? preference, string system, string expected) =>
        Assert.Equal(expected, AppLanguage.Resolve(preference, system));

    [Fact]
    public void EnglishAndKoreanResourcesHaveMatchingKeysAndPlaceholders()
    {
        var english = Load("Strings.resx");
        var korean = Load("Strings.ko.resx");
        Assert.Equal(english.Keys.OrderBy(key => key), korean.Keys.OrderBy(key => key));
        foreach (var pair in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(korean[pair.Key]));
            Assert.Equal(Placeholders(pair.Value), Placeholders(korean[pair.Key]));
        }
    }

    [Fact]
    public void EveryXamlTranslationKeyExists()
    {
        var english = Load("Strings.resx");
        foreach (var path in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Localization"), "*.xaml"))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(path), @"\{DynamicResource Text\.([A-Za-z0-9]+)\}"))
                Assert.True(english.ContainsKey(match.Groups[1].Value), path + ": " + match.Value);
        }
    }

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Localization", file))
            .Root!.Elements("data").ToDictionary(element => (string)element.Attribute("name")!,
                element => (string)element.Element("value")!);

    private static string[] Placeholders(string value) =>
        Regex.Matches(value, @"\{\d+(?::[^}]+)?\}").Cast<Match>().Select(match => match.Value).OrderBy(item => item).ToArray();
}
