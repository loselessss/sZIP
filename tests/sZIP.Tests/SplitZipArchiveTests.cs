using System.Reflection;
using System.Text;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class SplitZipArchiveTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "szip-splitzip-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Infozip.nocomp.multi", false)]
    [InlineData("Infozip.nocomp.multi", true)]
    [InlineData("WinZip26.nocomp.multi", false)]
    [InlineData("WinZip26.nocomp.multi", true)]
    public async Task SplitZip_ListsAndExtractsWithEitherFilenameCase(string fixture, bool uppercase)
    {
        var archive = CopyFixture(fixture, uppercase);
        var output = Path.Combine(_root, "output");
        var service = new MultiFormatArchiveService();
        var entries = await service.ListEntriesAsync(archive);
        await service.ExtractAsync(archive, output);
        var files = entries.Where(entry => !entry.IsDirectory).ToArray();
        Assert.NotEmpty(files);
        Assert.Equal(files.Length, Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length);
        foreach (var entry in files)
            Assert.Equal(entry.Length, new FileInfo(ArchivePath.GetSafeDestinationPath(output, entry.FullName)).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSplitPart_ExplainsHowToRecover(bool extract)
    {
        var archive = CopyFixture("Infozip.nocomp.multi", false);
        File.Delete(Path.ChangeExtension(archive, ".z01"));
        var service = new MultiFormatArchiveService();
        var error = await Assert.ThrowsAsync<IOException>(async () =>
        {
            if (extract) await service.ExtractAsync(archive, Path.Combine(_root, "output"));
            else await service.ListEntriesAsync(archive);
        });
        Assert.Contains("split ZIP part is missing", error.Message);
    }

    [Fact]
    public async Task IncompleteEntry_RemovesPartialOutput()
    {
        Directory.CreateDirectory(_root);
        var output = Path.Combine(_root, "partial.txt");
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        var copy = typeof(MultiFormatArchiveService).GetMethod("CopyEntryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task)copy.Invoke(null, new object[]
        {
            source, output, 4L, new Action<long>(_ => { }), CancellationToken.None
        })!;
        await Assert.ThrowsAsync<InvalidDataException>(() => task);
        Assert.False(File.Exists(output));
    }

    private string CopyFixture(string name, bool uppercase)
    {
        Directory.CreateDirectory(_root);
        foreach (var extension in new[] { ".z01", ".zip" })
        {
            var fileName = name + extension;
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SplitZip", fileName),
                Path.Combine(_root, uppercase ? fileName.ToUpperInvariant() : fileName));
        }
        return Path.Combine(_root, uppercase ? (name + ".zip").ToUpperInvariant() : name + ".zip");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
