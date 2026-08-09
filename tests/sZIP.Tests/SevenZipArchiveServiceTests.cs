using sZIP.Archive;

namespace sZIP.Tests;

public sealed class SevenZipArchiveServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-sevenzip-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CreateAndExtract_PreservesSubfoldersAndEmptyDirectory()
    {
        var source = Path.Combine(_testRoot, "자료");
        Directory.CreateDirectory(Path.Combine(source, "하위", "빈 폴더"));
        File.WriteAllText(Path.Combine(source, "하위", "문서.txt"), "sZIP 7Z 왕복 테스트");
        var archivePath = Path.Combine(_testRoot, "created.7z");
        var outputPath = Path.Combine(_testRoot, "output");

        await new SevenZipArchiveService().CreateAsync(archivePath, new[] { source });
        var reader = new MultiFormatArchiveService();
        var entries = await reader.ListEntriesAsync(archivePath);
        await reader.ExtractAsync(archivePath, outputPath);

        Assert.Contains(entries, entry => entry.FullName == "자료/하위/문서.txt");
        Assert.Contains(entries, entry => entry.FullName.TrimEnd('/') == "자료/하위/빈 폴더");
        Assert.Equal("sZIP 7Z 왕복 테스트", File.ReadAllText(
            Path.Combine(outputPath, "자료", "하위", "문서.txt")));
        Assert.True(Directory.Exists(Path.Combine(outputPath, "자료", "하위", "빈 폴더")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
