using sZIP.Archive;

namespace sZIP.Tests;

public sealed class ArchivePathTests
{
    [Fact]
    public void SafeChildPath_IsReturnedInsideDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "szip-tests", "output");

        var result = ArchivePath.GetSafeDestinationPath(root, "folder/file.txt");

        Assert.StartsWith(Path.GetFullPath(root), result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("folder", "file.txt"), result);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("folder/../../escape.txt")]
    [InlineData("C:/escape.txt")]
    [InlineData("file.txt:stream")]
    public void UnsafePath_IsRejected(string entryName)
    {
        var root = Path.Combine(Path.GetTempPath(), "szip-tests", "output");

        Assert.Throws<ArchiveSecurityException>(() =>
            ArchivePath.GetSafeDestinationPath(root, entryName));
    }
}
