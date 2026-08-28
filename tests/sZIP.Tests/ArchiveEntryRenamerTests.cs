using sZIP.Application;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class ArchiveEntryRenamerTests
{
    [Theory]
    [InlineData(".zip")]
    [InlineData(".7z")]
    public async Task RenameChangesAFileNameAndPreservesItsContent(string extension)
    {
        using var sandbox = new RenameSandbox();
        var source = sandbox.CreateFile("source/old.txt", "hello");
        var archivePath = Path.Combine(sandbox.Root, "archive" + extension);
        await sandbox.CreateArchiveAsync(archivePath, new[] { source });

        await sandbox.Renamer.RenameAsync(archivePath, "old.txt", "new.txt");

        var entries = await sandbox.Reader.ListEntriesAsync(archivePath);
        Assert.DoesNotContain(entries, entry => entry.FullName == "old.txt");
        Assert.Contains(entries, entry => entry.FullName == "new.txt");
        var output = Path.Combine(sandbox.Root, "output");
        await sandbox.Reader.ExtractAsync(archivePath, output);
        Assert.Equal("hello", File.ReadAllText(Path.Combine(output, "new.txt")));
    }

    [Theory]
    [InlineData(".zip")]
    [InlineData(".7z")]
    public async Task RenameChangesAFolderAndAllChildPaths(string extension)
    {
        using var sandbox = new RenameSandbox();
        var folder = sandbox.CreateDirectory("source/old-folder");
        sandbox.CreateFile("source/old-folder/child.txt", "child");
        var archivePath = Path.Combine(sandbox.Root, "archive" + extension);
        await sandbox.CreateArchiveAsync(archivePath, new[] { folder });

        await sandbox.Renamer.RenameAsync(archivePath, "old-folder", "new-folder");

        var entries = await sandbox.Reader.ListEntriesAsync(archivePath);
        Assert.DoesNotContain(entries, entry => entry.FullName.StartsWith("old-folder", StringComparison.Ordinal));
        Assert.Contains(entries, entry => entry.FullName == "new-folder/child.txt");
    }

    [Fact]
    public async Task RenameRejectsAConflictingNameWithoutChangingTheArchive()
    {
        using var sandbox = new RenameSandbox();
        var first = sandbox.CreateFile("source/first.txt", "first");
        var second = sandbox.CreateFile("source/second.txt", "second");
        var archivePath = Path.Combine(sandbox.Root, "archive.zip");
        await sandbox.CreateArchiveAsync(archivePath, new[] { first, second });

        await Assert.ThrowsAsync<IOException>(() =>
            sandbox.Renamer.RenameAsync(archivePath, "first.txt", "second.txt"));

        var entries = await sandbox.Reader.ListEntriesAsync(archivePath);
        Assert.Contains(entries, entry => entry.FullName == "first.txt");
        Assert.Contains(entries, entry => entry.FullName == "second.txt");
    }

    private sealed class RenameSandbox : IDisposable
    {
        private readonly ZipArchiveService _zip = new();
        private readonly SevenZipArchiveService _sevenZip = new();

        public RenameSandbox()
        {
            Root = Path.Combine(Path.GetTempPath(), "sZIP-rename-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Reader = new MultiFormatArchiveService();
            Renamer = new ArchiveEntryRenamer(Reader, _zip, _sevenZip);
        }

        public string Root { get; }
        public MultiFormatArchiveService Reader { get; }
        public ArchiveEntryRenamer Renamer { get; }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public Task CreateArchiveAsync(string path, IReadOnlyCollection<string> sources) =>
            string.Equals(Path.GetExtension(path), ".7z", StringComparison.OrdinalIgnoreCase)
                ? _sevenZip.CreateAsync(path, sources)
                : _zip.CreateAsync(path, sources);

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
