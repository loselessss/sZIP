using System.Text;
using SharpCompress.Common;
using SharpCompress.Writers;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class MultiFormatArchiveServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-multiformat-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("sample.tar", false)]
    [InlineData("sample.tar.gz", true)]
    public async Task ListAndExtract_TarFamily_PreservesSubfoldersAndContent(
        string fileName,
        bool gzip)
    {
        Directory.CreateDirectory(_testRoot);
        var archivePath = Path.Combine(_testRoot, fileName);
        CreateTarArchive(archivePath, "folder/subfolder/hello.txt", "안녕하세요 sZIP", gzip);
        var outputPath = Path.Combine(_testRoot, "output-" + Guid.NewGuid().ToString("N"));
        var service = new MultiFormatArchiveService();

        var entries = await service.ListEntriesAsync(archivePath);
        await service.ExtractAsync(archivePath, outputPath);

        var entry = Assert.Single(entries, item => !item.IsDirectory);
        Assert.Equal("folder/subfolder/hello.txt", entry.FullName);
        Assert.Equal("안녕하세요 sZIP", File.ReadAllText(
            Path.Combine(outputPath, "folder", "subfolder", "hello.txt")));
    }

    [Theory]
    [InlineData("archive.zip", true)]
    [InlineData("archive.7z", true)]
    [InlineData("archive.rar", true)]
    [InlineData("archive.tar.gz", true)]
    [InlineData("archive.txt", false)]
    public void Supports_RecognizesConfiguredExtensions(string fileName, bool expected)
    {
        Assert.Equal(expected, new MultiFormatArchiveService().Supports(fileName));
    }

    [Fact]
    public async Task ExtractSelected_ExtractsOnlyRequestedEntriesAndKeepsFolders()
    {
        Directory.CreateDirectory(_testRoot);
        var archivePath = Path.Combine(_testRoot, "selected.tar");
        CreateTarArchive(archivePath, new Dictionary<string, string>
        {
            ["docs/keep.txt"] = "keep",
            ["docs/skip.txt"] = "skip",
            ["root.txt"] = "root"
        });
        var outputPath = Path.Combine(_testRoot, "selected-output");
        var service = new MultiFormatArchiveService();

        await service.ExtractSelectedAsync(
            archivePath, outputPath, new[] { "docs/keep.txt", "root.txt" });

        Assert.Equal("keep", File.ReadAllText(Path.Combine(outputPath, "docs", "keep.txt")));
        Assert.Equal("root", File.ReadAllText(Path.Combine(outputPath, "root.txt")));
        Assert.False(File.Exists(Path.Combine(outputPath, "docs", "skip.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static void CreateTarArchive(
        string archivePath,
        string entryName,
        string content,
        bool gzip)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        using var tarStream = new MemoryStream();
        using (var writer = WriterFactory.OpenWriter(
                   tarStream,
                   ArchiveType.Tar,
                   new WriterOptions(CompressionType.None) { LeaveStreamOpen = true }))
        using (var contentStream = new MemoryStream(contentBytes, writable: false))
        {
            writer.Write(entryName, contentStream, DateTime.UtcNow);
        }

        tarStream.Position = 0;
        using var file = File.Create(archivePath);
        if (!gzip)
        {
            tarStream.CopyTo(file);
            return;
        }

        using var gzipWriter = WriterFactory.OpenWriter(
            file,
            ArchiveType.GZip,
            WriterOptions.ForGZip());
        gzipWriter.Write("sample.tar", tarStream, DateTime.UtcNow);
    }

    private static void CreateTarArchive(
        string archivePath,
        IReadOnlyDictionary<string, string> entries)
    {
        using var file = File.Create(archivePath);
        using var writer = WriterFactory.OpenWriter(
            file,
            ArchiveType.Tar,
            new WriterOptions(CompressionType.None));
        foreach (var entry in entries)
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(entry.Value), writable: false);
            writer.Write(entry.Key, content, DateTime.UtcNow);
        }
    }
}
