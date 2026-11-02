using System.IO.Compression;
using sZIP.Watcher;

namespace sZIP.Tests;

public sealed class RecursiveArchiveWatcherTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-recursive-watcher-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ManualScanFindsOldArchivesAndPreservesExclusionsAndDeduplication()
    {
        Directory.CreateDirectory(_testRoot);
        foreach (var name in new[] { "old.zip", "processed.zip", "excluded.zip" })
        {
            var path = Path.Combine(_testRoot, name);
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create)) archive.CreateEntry("file.txt");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
        }
        using var watcher = new RecursiveArchiveWatcher(new ArchiveWatchOptions(_testRoot,
            stableFor: TimeSpan.FromMilliseconds(30), probeInterval: TimeSpan.FromMilliseconds(10),
            reconcileInterval: TimeSpan.FromMilliseconds(40)));
        using var exclusion = watcher.ExcludePath(Path.Combine(_testRoot, "excluded.zip"));
        watcher.MarkProcessed(Path.Combine(_testRoot, "processed.zip"));
        var ready = new TaskCompletionSource<string>();
        var count = 0;
        watcher.ArchiveReady += (_, path) => { Interlocked.Increment(ref count); ready.TrySetResult(path); };
        watcher.Start();
        await Task.Delay(150);
        Assert.False(ready.Task.IsCompleted);
        await watcher.ScanExistingAsync();
        Assert.Same(ready.Task, await Task.WhenAny(ready.Task, Task.Delay(3000)));
        Assert.Equal(Path.Combine(_testRoot, "old.zip"), await ready.Task);
        await watcher.ScanExistingAsync();
        await Task.Delay(150);
        Assert.Equal(1, Volatile.Read(ref count));
    }

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

    [Fact]
    public async Task AppCreatedZip_IsSkippedButOtherZipIsStillDetected()
    {
        Directory.CreateDirectory(_testRoot);
        var options = new ArchiveWatchOptions(
            _testRoot,
            stableFor: TimeSpan.FromMilliseconds(30),
            probeInterval: TimeSpan.FromMilliseconds(10),
            reconcileInterval: TimeSpan.FromMilliseconds(40));
        var createdByApp = Path.Combine(_testRoot, "created-by-app.zip");
        var downloaded = Path.Combine(_testRoot, "downloaded.zip");
        var ready = new TaskCompletionSource<string>();
        var raisedPaths = new List<string>();

        using (var watcher = new RecursiveArchiveWatcher(options))
        {
            watcher.ArchiveReady += (_, path) =>
            {
                lock (raisedPaths) raisedPaths.Add(path);
                ready.TrySetResult(path);
            };
            watcher.Start();

            using (watcher.ExcludePath(createdByApp))
            {
                using (var archive = ZipFile.Open(createdByApp, ZipArchiveMode.Create))
                {
                    archive.CreateEntry("file.txt");
                }
                watcher.MarkProcessed(createdByApp);
            }

            using (var archive = ZipFile.Open(downloaded, ZipArchiveMode.Create))
            {
                archive.CreateEntry("file.txt");
            }

            var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(ready.Task, completed);
            Assert.Equal(downloaded, await ready.Task);
            await Task.Delay(180);
            lock (raisedPaths) Assert.Equal(new[] { downloaded }, raisedPaths);
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public async Task LongReconciliationInterval_DetectsNewArchivesAndCanStop(int seconds)
    {
        Directory.CreateDirectory(_testRoot);
        var options = new ArchiveWatchOptions(
            _testRoot,
            stableFor: TimeSpan.FromMilliseconds(30),
            probeInterval: TimeSpan.FromMilliseconds(10),
            reconcileInterval: TimeSpan.FromSeconds(seconds));
        var ready = new TaskCompletionSource<string>();
        using (var watcher = new RecursiveArchiveWatcher(options))
        {
            watcher.ArchiveReady += (_, path) => ready.TrySetResult(path);
            watcher.Start();
            var archivePath = Path.Combine(_testRoot, "new.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("file.txt");
            }
            var completed = await Task.WhenAny(ready.Task, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.Same(ready.Task, completed);
            Assert.Equal(archivePath, await ready.Task);
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
