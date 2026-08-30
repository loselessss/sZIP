using System.Xml.Linq;

namespace sZIP.Tests;

public sealed class ShellPackageTests
{
    [Fact]
    public void UnsignedPackageAndApplicationUseMatchingIdentity()
    {
        var package = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Shell", "AppxManifest.xml"));
        var app = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Shell", "app.manifest"));
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace msix = "urn:schemas-microsoft-com:msix.v1";
        var identity = package.Root!.Element(ns + "Identity")!;
        var appIdentity = app.Root!.Element(msix + "msix")!;
        var publisher = (string)identity.Attribute("Publisher")!;
        Assert.Contains("OID.2.25.311729368913984317654407730594956997722=1", publisher);
        Assert.Equal(publisher, (string)appIdentity.Attribute("publisher"));
        Assert.Equal((string)identity.Attribute("Name"), (string)appIdentity.Attribute("packageName"));
        Assert.Equal((string)identity.Attribute("Version"), (string)app.Root.Elements().First(e => e.Name.LocalName == "assemblyIdentity").Attribute("version"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("Directory")]
    public void ModernMenuRegistersFilesAndFolders(string type)
    {
        var package = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Shell", "AppxManifest.xml"));
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/5";
        XNamespace com = "http://schemas.microsoft.com/appx/manifest/com/windows10";
        var item = Assert.Single(package.Descendants(desktop + "ItemType"), e => (string)e.Attribute("Type")! == type);
        var verb = Assert.Single(item.Elements(desktop + "Verb"));
        Assert.Contains(package.Descendants(com + "Class"), e => (string)e.Attribute("Id")! == (string)verb.Attribute("Clsid")!);
    }
}
