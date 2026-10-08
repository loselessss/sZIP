using System.IO.Compression;
using sZIP.Application;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class ArchiveWorkspaceTests
{
    [Fact]
    public async Task CloseClearsArchiveAndPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create)) archive.CreateEntry("file.txt");
            var workspace = new ArchiveWorkspace(new MultiFormatArchiveService());
            await workspace.OpenAsync(path, "password");
            Assert.Equal(path, workspace.CurrentArchivePath);
            Assert.Equal("password", workspace.CurrentPassword);
            workspace.Close();
            Assert.Null(workspace.CurrentArchivePath);
            Assert.Null(workspace.CurrentPassword);
            await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ExtractAsync("unused"));
        }
        finally { File.Delete(path); }
    }
}
