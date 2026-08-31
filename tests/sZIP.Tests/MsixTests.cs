using System.Configuration;
using System.Xml;
using System.Xml.Linq;
using sZIP.App;

namespace sZIP.Tests;

public sealed class MsixTests
{
    [Theory]
    [InlineData(false, null, DistributionChannel.Exe)]
    [InlineData(false, "<Distribution Channel='Store'/>", DistributionChannel.Exe)]
    [InlineData(true, null, DistributionChannel.Unconfigured)]
    [InlineData(true, "broken xml", DistributionChannel.Unconfigured)]
    [InlineData(true, "<Distribution Channel='Other'/>", DistributionChannel.Unconfigured)]
    [InlineData(true, "<Distribution Channel='Store'/>", DistributionChannel.Store)]
    [InlineData(true, "<Distribution Channel='Direct' AppInstallerUri='https://example.org/sZIP.appinstaller'/>", DistributionChannel.Direct)]
    [InlineData(true, "<Distribution Channel='Direct' AppInstallerUri='http://example.org/sZIP.appinstaller'/>", DistributionChannel.Unconfigured)]
    [InlineData(true, "<Distribution Channel='Direct' AppInstallerUri='file:///C:/installer.exe'/>", DistributionChannel.Unconfigured)]
    [InlineData(true, "<Distribution Channel='Direct' AppInstallerUri='https://example.org/sZIP.exe'/>", DistributionChannel.Unconfigured)]
    [InlineData(true, "<Distribution Channel='Direct' AppInstallerUri='https://user:password@example.org/sZIP.appinstaller'/>", DistributionChannel.Unconfigured)]
    [InlineData(true, "<!DOCTYPE Distribution [<!ENTITY test SYSTEM 'file:///C:/secret.txt'>]><Distribution Channel='Store'>&test;</Distribution>", DistributionChannel.Unconfigured)]
    public void DistributionCannotFallBackToExeUpdates(bool packaged, string? xml, object expected) =>
        Assert.Equal(expected, PackageDistribution.Read(packaged, xml).Channel);

    [Fact]
    public void StoreUpdateUsesStoreNotGitHub() =>
        Assert.Equal("ms-windows-store://downloadsandupdates/",
            PackageDistribution.Read(true, "<Distribution Channel='Store'/>").UpdateUri!.AbsoluteUri);

    [Fact]
    public void FullPackageOwnsAppAndExplorerAndDoesNotForceStartupOrDefaults()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Packaging", "AppxManifest.xml"));
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";
        Assert.Empty(document.Descendants(uap10 + "AllowExternalContent"));
        Assert.DoesNotContain("OID.2.25.", (string)document.Root!.Element(ns + "Identity")!.Attribute("Publisher")!);
        Assert.Equal("x64", (string)document.Root.Element(ns + "Identity")!.Attribute("ProcessorArchitecture"));
        Assert.Equal("packagedClassicApp", (string)Assert.Single(document.Descendants(ns + "Application")).Attribute(uap10 + "RuntimeBehavior"));
        Assert.Equal("false", (string)Assert.Single(document.Descendants(desktop + "StartupTask")).Attribute("Enabled"));
        Assert.Equal("--tray", (string)Assert.Single(document.Descendants(desktop + "Extension")).Attribute(uap10 + "Parameters"));
        Assert.DoesNotContain(document.Descendants().Attributes(), e => e.Name.LocalName == "AllowSilentDefaultTakeOver");
        Assert.DoesNotContain(document.Descendants(), e => (string?)e.Attribute("Name") == "unvirtualizedResources");
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        Assert.Equal(new[] { ".7z", ".gz", ".rar", ".tar", ".tgz", ".zip" },
            document.Descendants(uap + "FileType").Select(e => e.Value).OrderBy(v => v));
    }

    [Fact]
    public void SettingsSurviveProviderRecreationAndPreserveUnicode()
    {
        var directory = Path.Combine(Path.GetTempPath(), "szip-msix-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.xml");
        try
        {
            var property = new SettingsProperty("Folder")
            {
                PropertyType = typeof(string), SerializeAs = SettingsSerializeAs.String, DefaultValue = ""
            };
            var properties = new SettingsPropertyCollection { property };
            var first = new MsixSettingsProvider(path);
            var values = new SettingsPropertyValueCollection
            {
                new SettingsPropertyValue(property) { PropertyValue = @"D:\다운로드 & 문서" }
            };
            first.SetPropertyValues(new SettingsContext(), values);
            var reopened = new MsixSettingsProvider(path);
            Assert.Equal(@"D:\다운로드 & 문서", reopened.GetPropertyValues(new SettingsContext(), properties)["Folder"].PropertyValue);
            values["Folder"].PropertyValue = @"D:\다음 버전";
            reopened.SetPropertyValues(new SettingsContext(), values);
            Assert.Equal(@"D:\다음 버전", new MsixSettingsProvider(path).GetPropertyValues(new SettingsContext(), properties)["Folder"].PropertyValue);
            Assert.True(File.Exists(path + ".backup"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
