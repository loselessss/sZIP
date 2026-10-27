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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAndExtract_PreservesEmptyFileOrDirectory(bool directoryOnly)
    {
        Directory.CreateDirectory(_testRoot);
        var source = Path.Combine(_testRoot, directoryOnly ? "empty-folder" : "empty.txt");
        if (directoryOnly) Directory.CreateDirectory(source);
        else File.WriteAllBytes(source, Array.Empty<byte>());
        var archive = Path.Combine(_testRoot, "empty.7z");
        await new SevenZipArchiveService().CreateAsync(archive, new[] { source });
        var service = new MultiFormatArchiveService();
        var entry = Assert.Single(await service.ListEntriesAsync(archive));
        Assert.Equal(directoryOnly, entry.IsDirectory);
        var output = Path.Combine(_testRoot, "output");
        await service.ExtractAsync(archive, output);
        var extracted = Path.Combine(output, Path.GetFileName(source));
        if (directoryOnly) Assert.True(Directory.Exists(extracted));
        else Assert.Equal(0, new FileInfo(extracted).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
