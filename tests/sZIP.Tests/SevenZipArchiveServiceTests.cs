using sZIP.Archive;

namespace sZIP.Tests;

public sealed class SevenZipArchiveServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-sevenzip-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateAndExtract_PreservesSubfoldersAndEmptyDirectory()
    {
        var source = Path.Combine(_testRoot, "Cafe");
        Directory.CreateDirectory(Path.Combine(source, "Resume", "Empty Folder"));
        File.WriteAllText(Path.Combine(source, "Resume", "notes.txt"), "sZIP 7Z round trip test");
        var archivePath = Path.Combine(_testRoot, "created.7z");
        var outputPath = Path.Combine(_testRoot, "output");

        await new SevenZipArchiveService().CreateAsync(archivePath, new[] { source });
        var reader = new MultiFormatArchiveService();
        var entries = await reader.ListEntriesAsync(archivePath);
        await reader.ExtractAsync(archivePath, outputPath);

        Assert.Contains(entries, entry => entry.FullName == "Cafe/Resume/notes.txt");
        Assert.Contains(entries, entry => entry.FullName.TrimEnd('/') == "Cafe/Resume/Empty Folder");
        Assert.Equal("sZIP 7Z round trip test", File.ReadAllText(
            Path.Combine(outputPath, "Cafe", "Resume", "notes.txt")));
        Assert.True(Directory.Exists(Path.Combine(outputPath, "Cafe", "Resume", "Empty Folder")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
