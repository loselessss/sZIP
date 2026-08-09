using System.IO.Compression;
using sZIP.Watcher;

namespace sZIP.Tests;

public sealed class ArchiveStabilityProbeTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "szip-watcher-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DefaultReconcileInterval_IsTenSeconds()
    {
        var options = new ArchiveWatchOptions(_testRoot);

        Assert.Equal(TimeSpan.FromSeconds(10), options.ReconcileInterval);
    }

    [Fact]
    public async Task StableZip_IsAccepted()
    {
        Directory.CreateDirectory(_testRoot);
        var path = Path.Combine(_testRoot, "ready.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            archive.CreateEntry("empty.txt");
        }

        var options = new ArchiveWatchOptions(
            _testRoot,
            stableFor: TimeSpan.FromMilliseconds(30),
            probeInterval: TimeSpan.FromMilliseconds(10));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var result = await ArchiveStabilityProbe.WaitUntilReadyAsync(path, options, timeout.Token);

        Assert.True(result);
    }

    [Fact]
    public async Task FileWithZipExtensionButWrongSignature_IsRejected()
    {
        Directory.CreateDirectory(_testRoot);
        var path = Path.Combine(_testRoot, "fake.zip");
        File.WriteAllText(path, "not a zip");
        var options = new ArchiveWatchOptions(
            _testRoot,
            stableFor: TimeSpan.FromMilliseconds(20),
            probeInterval: TimeSpan.FromMilliseconds(10));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var result = await ArchiveStabilityProbe.WaitUntilReadyAsync(path, options, timeout.Token);

        Assert.False(result);
    }

    [Fact]
    public async Task StableSupportedNonZip_WhenSignatureCheckDisabled_IsAccepted()
    {
        Directory.CreateDirectory(_testRoot);
        var path = Path.Combine(_testRoot, "ready.7z");
        File.WriteAllText(path, "stable test payload");
        var options = new ArchiveWatchOptions(
            _testRoot,
            stableFor: TimeSpan.FromMilliseconds(20),
            probeInterval: TimeSpan.FromMilliseconds(10),
            supportedExtensions: new[] { ".zip", ".7z" },
            requireZipSignature: false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var result = await ArchiveStabilityProbe.WaitUntilReadyAsync(path, options, timeout.Token);

        Assert.True(options.Supports(path));
        Assert.True(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
