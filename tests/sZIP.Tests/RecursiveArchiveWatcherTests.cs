using System.IO.Compression;
using sZIP.Watcher;

namespace sZIP.Tests;

public sealed class RecursiveArchiveWatcherTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-recursive-watcher-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task NewZipInSubfolder_IsRaisedOnlyOnceAcrossReconciliation()
    {
        Directory.CreateDirectory(Path.Combine(_testRoot, "nested", "download"));
        var options = new ArchiveWatchOptions(
            _testRoot,
            stableFor: TimeSpan.FromMilliseconds(30),
            probeInterval: TimeSpan.FromMilliseconds(10),
            reconcileInterval: TimeSpan.FromMilliseconds(40));
        var ready = new TaskCompletionSource<string>();
        var raisedCount = 0;

        using (var watcher = new RecursiveArchiveWatcher(options))
        {
            watcher.ArchiveReady += (_, path) =>
            {
                Interlocked.Increment(ref raisedCount);
                ready.TrySetResult(path);
            };
            watcher.Start();

            var archivePath = Path.Combine(_testRoot, "nested", "download", "new.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("file.txt");
            }

            var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(ready.Task, completed);
            Assert.Equal(archivePath, await ready.Task);
            await Task.Delay(180);
            Assert.Equal(1, Volatile.Read(ref raisedCount));
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
