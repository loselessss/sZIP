using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace sZIP.App;

internal enum DistributionChannel { Exe, Store, Direct, Unconfigured }

internal sealed class PackageDistribution
{
    private PackageDistribution(DistributionChannel channel, Uri? updateUri = null)
    {
        Channel = channel;
        UpdateUri = updateUri;
    }

    public DistributionChannel Channel { get; }
    public Uri? UpdateUri { get; }

    public static PackageDistribution Read(bool packaged, string? configuration)
    {
        if (!packaged) return new PackageDistribution(DistributionChannel.Exe);
        try
        {
            using var text = new StringReader(configuration ?? string.Empty);
            using var reader = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16384
            });
            var root = XDocument.Load(reader).Root;
            if (root?.Name != "Distribution") return new PackageDistribution(DistributionChannel.Unconfigured);
            var channel = (string?)root.Attribute("Channel");
            if (channel == "Store")
                return new PackageDistribution(DistributionChannel.Store, new Uri("ms-windows-store://downloadsandupdates"));
            if (channel == "Direct" && Uri.TryCreate((string?)root.Attribute("AppInstallerUri"), UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath.EndsWith(".appinstaller", StringComparison.OrdinalIgnoreCase))
                return new PackageDistribution(DistributionChannel.Direct, uri);
        }
        catch (XmlException) { }
        // A packaged app must never fall back to the EXE updater on missing/invalid configuration.
        return new PackageDistribution(DistributionChannel.Unconfigured);
    }
}
