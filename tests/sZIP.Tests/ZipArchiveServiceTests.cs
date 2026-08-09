using System.IO.Compression;
using System.Text;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class ZipArchiveServiceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ListAndExtract_PreservesSubfoldersAndContent()
    {
        Directory.CreateDirectory(_testRoot);
        var archivePath = Path.Combine(_testRoot, "sample.zip");
        await CreateArchiveAsync(archivePath, "folder/hello.txt", "안녕하세요 sZIP");
        var outputPath = Path.Combine(_testRoot, "output");
        var service = new ZipArchiveService();

        var entries = await service.ListEntriesAsync(archivePath);
        await service.ExtractAsync(archivePath, outputPath);

        var entry = Assert.Single(entries);
        Assert.Equal("folder/hello.txt", entry.FullName);
        Assert.Equal("안녕하세요 sZIP", File.ReadAllText(
            Path.Combine(outputPath, "folder", "hello.txt")));
    }

    [Fact]
    public async Task Extract_WhenFileExists_CreatesUniqueName()
    {
        Directory.CreateDirectory(_testRoot);
        var archivePath = Path.Combine(_testRoot, "sample.zip");
        await CreateArchiveAsync(archivePath, "hello.txt", "new");
        var outputPath = Path.Combine(_testRoot, "output");
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, "hello.txt"), "existing");
        var service = new ZipArchiveService();

        await service.ExtractAsync(archivePath, outputPath);

        Assert.Equal("existing", File.ReadAllText(Path.Combine(outputPath, "hello.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(outputPath, "hello (1).txt")));
    }

    [Fact]
    public async Task Extract_ZipSlipEntry_IsRejectedBeforeWriting()
    {
        Directory.CreateDirectory(_testRoot);
        var archivePath = Path.Combine(_testRoot, "unsafe.zip");
        await CreateArchiveAsync(archivePath, "../escape.txt", "unsafe");
        var outputPath = Path.Combine(_testRoot, "output");
        var service = new ZipArchiveService();

        await Assert.ThrowsAsync<ArchiveSecurityException>(() =>
            service.ExtractAsync(archivePath, outputPath));

        Assert.False(File.Exists(Path.Combine(_testRoot, "escape.txt")));
    }

    [Fact]
    public async Task Create_FromDirectory_PreservesRootSubfoldersAndEmptyDirectory()
    {
        var source = Path.Combine(_testRoot, "자료");
        Directory.CreateDirectory(Path.Combine(source, "하위", "빈 폴더"));
        File.WriteAllText(Path.Combine(source, "하위", "문서.txt"), "sZIP 생성 테스트");
        var archivePath = Path.Combine(_testRoot, "created.zip");
        var service = new ZipArchiveService();

        await service.CreateAsync(archivePath, new[] { source });

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            Assert.Contains(archive.Entries, entry => entry.FullName == "자료/하위/문서.txt");
            Assert.Contains(archive.Entries, entry => entry.FullName == "자료/하위/빈 폴더/");
            var document = Assert.Single(
                archive.Entries, entry => entry.FullName == "자료/하위/문서.txt");
            using (var reader = new StreamReader(document.Open(), Encoding.UTF8))
            {
                Assert.Equal("sZIP 생성 테스트", reader.ReadToEnd());
            }
        }
    }

    [Fact]
    public async Task Create_WhenOutputIsInsideSource_DoesNotIncludeItself()
    {
        var source = Path.Combine(_testRoot, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "input.txt"), "content");
        var archivePath = Path.Combine(source, "source.zip");
        File.WriteAllText(archivePath, "old archive placeholder");
        var service = new ZipArchiveService();

        await service.CreateAsync(archivePath, new[] { source });

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            Assert.Contains(archive.Entries, entry => entry.FullName == "source/input.txt");
            Assert.DoesNotContain(archive.Entries, entry => entry.FullName.EndsWith("source.zip"));
        }
    }

    [Fact]
    public async Task Create_DuplicateTopLevelFileNames_UsesUniqueEntryNames()
    {
        var firstFolder = Path.Combine(_testRoot, "first");
        var secondFolder = Path.Combine(_testRoot, "second");
        Directory.CreateDirectory(firstFolder);
        Directory.CreateDirectory(secondFolder);
        var first = Path.Combine(firstFolder, "same.txt");
        var second = Path.Combine(secondFolder, "same.txt");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        var archivePath = Path.Combine(_testRoot, "duplicates.zip");
        var service = new ZipArchiveService();

        await service.CreateAsync(archivePath, new[] { first, second });

        using (var archive = ZipFile.OpenRead(archivePath))
        {
            Assert.Contains(archive.Entries, entry => entry.FullName == "same.txt");
            Assert.Contains(archive.Entries, entry => entry.FullName == "same (1).txt");
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static async Task CreateArchiveAsync(
        string archivePath,
        string entryName,
        string content)
    {
        using (var file = File.Create(archivePath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
        {
            var entry = archive.CreateEntry(entryName);
            using (var entryStream = entry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                await entryStream.WriteAsync(bytes, 0, bytes.Length);
            }
        }
    }
}
