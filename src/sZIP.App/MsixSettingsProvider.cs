using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace sZIP.App;

// Package versions install in different directories. Keep settings under the stable package family.
internal sealed class MsixSettingsProvider : SettingsProvider
{
    private readonly string _path;
    public MsixSettingsProvider(string path)
    {
        _path = path;
        Initialize("sZIP.MSIX", new System.Collections.Specialized.NameValueCollection());
    }
    public override string ApplicationName { get; set; } = "sZIP";

    private XDocument Read()
    {
        if (!File.Exists(_path)) return new XDocument(new XElement("Settings"));
        using var reader = XmlReader.Create(_path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1048576
        });
        var document = XDocument.Load(reader);
        if (document.Root?.Name != "Settings") throw new ConfigurationErrorsException("Invalid MSIX settings document.");
        return document;
    }

    public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
    {
        var root = Read().Root!;
        var result = new SettingsPropertyValueCollection();
        foreach (SettingsProperty property in properties)
        {
            var element = root.Elements("Setting").FirstOrDefault(e => (string?)e.Attribute("Name") == property.Name);
            result.Add(new SettingsPropertyValue(property)
            {
                SerializedValue = element is null ? property.DefaultValue : element.Value,
                IsDirty = false
            });
        }
        return result;
    }

    public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection properties)
    {
        var document = Read();
        foreach (SettingsPropertyValue value in properties)
        {
            document.Root!.Elements("Setting").Where(e => (string?)e.Attribute("Name") == value.Name).Remove();
            document.Root.Add(new XElement("Setting", new XAttribute("Name", value.Name), value.SerializedValue ?? string.Empty));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            document.Save(temporary);
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".backup");
            else File.Move(temporary, _path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
