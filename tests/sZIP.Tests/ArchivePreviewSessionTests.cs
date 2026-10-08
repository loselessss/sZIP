using System.IO.Compression;
using sZIP.Application;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class ArchivePreviewSessionTests
{
    [Fact]
    public async Task PreviewExtractsOnlyRequestedFileAndPreservesArchive()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("nested/hello.txt").Open()))
                    writer.Write("미리보기 test");
                archive.CreateEntry("other.txt");
            }
            var original = File.ReadAllBytes(archivePath);
            string previewDirectory;
            using (var session = new ArchivePreviewSession())
            {
                previewDirectory = session.DirectoryPath;
                var preview = await session.PrepareAsync(new MultiFormatArchiveService(), archivePath,
                    "nested/hello.txt", null, null, CancellationToken.None);
                Assert.Equal("미리보기 test", File.ReadAllText(preview));
                Assert.Single(Directory.GetFiles(previewDirectory, "*", SearchOption.AllDirectories));
                Assert.Equal(original, File.ReadAllBytes(archivePath));
            }
            Assert.False(Directory.Exists(previewDirectory));
        }
        finally { File.Delete(archivePath); }
    }

    [Fact]
    public async Task CancelledPreviewCanBeCleanedUp()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create)) archive.CreateEntry("file.txt");
            string previewDirectory;
            using (var session = new ArchivePreviewSession())
            {
                previewDirectory = session.DirectoryPath;
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.PrepareAsync(
                    new MultiFormatArchiveService(), archivePath, "file.txt", null, null, new CancellationToken(true)));
            }
            Assert.False(Directory.Exists(previewDirectory));
        }
        finally { File.Delete(archivePath); }
    }

    [Fact]
    public async Task UnsafeEntryCannotEscapePreviewDirectory()
    {
        using var session = new ArchivePreviewSession();
        await Assert.ThrowsAsync<ArchiveSecurityException>(() => session.PrepareAsync(
            new MultiFormatArchiveService(), "unused.zip", "../outside.txt", null, null, CancellationToken.None));
    }
}
