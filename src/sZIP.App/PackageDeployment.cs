using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace sZIP.App;

internal static class PackageDeployment
{
    private static readonly Lazy<string?> Family = new(ReadFamilyName);
    private static readonly Lazy<PackageDistribution> Configuration = new(ReadConfiguration);
    public static bool IsPackaged => Family.Value is not null;
    public static PackageDistribution Distribution => Configuration.Value;
    public static string InstanceSuffix => IsPackaged ? ".MSIX." + Family.Value : string.Empty;
    public static string DataDirectory => IsPackaged
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sZIP", "MSIX", Family.Value!)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sZIP");

    private static PackageDistribution ReadConfiguration()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MsixDistribution.xml");
        try { return PackageDistribution.Read(IsPackaged, File.Exists(path) ? File.ReadAllText(path) : null); }
        catch (IOException) { return PackageDistribution.Read(IsPackaged, null); }
        catch (UnauthorizedAccessException) { return PackageDistribution.Read(IsPackaged, null); }
    }

    private static string? ReadFamilyName()
    {
        try
        {
            uint length = 0;
            var result = GetCurrentPackageFamilyName(ref length, null);
            if (result == 15700) return null; // APPMODEL_ERROR_NO_PACKAGE
            if (result != 122 || length == 0 || length > 256) throw new Win32Exception(result);
            var name = new StringBuilder((int)length);
            result = GetCurrentPackageFamilyName(ref length, name);
            if (result != 0) throw new Win32Exception(result);
            return name.ToString();
        }
        catch (EntryPointNotFoundException) { return null; } // Windows 7 EXE remains supported.
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder? packageFamilyName);
}
