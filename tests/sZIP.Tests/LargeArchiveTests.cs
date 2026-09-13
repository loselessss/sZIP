using System.IO.Compression;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class LargeArchiveTests
{
    [Fact]
    public async Task ManualExtraction_AllowsManyEntriesAndHighExpansion()
    {
        var root = Path.Combine(Path.GetTempPath(), "szip-limits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "many.zip");
            var payload = new byte[1024 * 1024];
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                for (var i = 0; i < 10001; i++) zip.CreateEntry("folder" + i + "/");
                using var output = zip.CreateEntry("data.bin", CompressionLevel.Optimal).Open();
                output.Write(payload, 0, payload.Length);
            }
            using (var zip = ZipFile.OpenRead(path))
            {
                new ExtractionPolicy().Validate(zip.Entries);
                Assert.Throws<ArchiveSecurityException>(() => new ExtractionPolicy(maxEntryCount: 10000).Validate(zip.Entries));
                Assert.Throws<ArchiveSecurityException>(() => new ExtractionPolicy(maxExpansionRatio: 20).Validate(zip.Entries));
            }
            var service = new MultiFormatArchiveService();
            Assert.Equal(10002, (await service.ListEntriesAsync(path)).Count);
            await service.ExtractSelectedAsync(path, Path.Combine(root, "output"), new[] { "data.bin" });
            Assert.Equal(payload, File.ReadAllBytes(Path.Combine(root, "output", "data.bin")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // Metadata-only fixture: cancel before reading the absent large payload.
    [Theory]
    [InlineData(1073741825L, 1)]
    [InlineData(2147483649L, 1)]
    [InlineData(805306368L, 3)]
    public async Task LargeEntries_PassSizeChecks(long size, int count)
    {
        var root = Path.Combine(Path.GetTempPath(), "szip-large-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "large.zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                for (var i = 0; i < count; i++) zip.CreateEntry(i + ".bin");
            var bytes = File.ReadAllBytes(path);
            for (var i = 0; i <= bytes.Length - 46; i++)
            {
                if (BitConverter.ToUInt32(bytes, i) != 0x02014b50) continue;
                Array.Copy(BitConverter.GetBytes((uint)size), 0, bytes, i + 20, 4);
                Array.Copy(BitConverter.GetBytes((uint)size), 0, bytes, i + 24, 4);
            }
            File.WriteAllBytes(path, bytes);
            using (var zip = ZipFile.OpenRead(path))
            {
                new ExtractionPolicy().Validate(zip.Entries);
                Assert.Throws<ArchiveSecurityException>(() => new ExtractionPolicy(
                    maxTotalBytes: 2147483648L, maxSingleFileBytes: 1073741824L).Validate(zip.Entries));
            }
            var service = new MultiFormatArchiveService();
            var entries = await service.ListEntriesAsync(path);
            Assert.Equal(size * count, entries.Sum(entry => entry.Length));
            var cancelled = new CancellationToken(true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.ExtractAsync(path, Path.Combine(root, "all"), cancellationToken: cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.ExtractSelectedAsync(path, Path.Combine(root, "selected"), new[] { "0.bin" },
                    cancellationToken: cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new ZipArchiveService().ExtractAsync(path, Path.Combine(root, "zip"),
                    cancellationToken: cancelled));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
