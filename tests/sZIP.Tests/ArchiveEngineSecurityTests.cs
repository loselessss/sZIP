using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using sZIP.Archive;

namespace sZIP.Tests;

public sealed class ArchiveEngineSecurityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TruncatedLzma2ChunkFailsInsteadOfLooping(int partialHeaderBytes)
    {
        // A complete reset chunk followed by an incomplete next chunk header.
        var bytes = new byte[] { 1, 0, 0, 88, 2, 0 };
        using var input = new MemoryStream(bytes, 0, 4 + partialHeaderBytes);
        using var decoder = LzmaStream.Create(new byte[] { 0 }, input);
        Assert.ThrowsAny<SharpCompressException>(() => decoder.CopyTo(Stream.Null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedSevenZipHeaderIsRejectedBeforeAllocation(bool extract)
    {
        var root = Path.Combine(Path.GetTempPath(), "szip-header-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var header = new byte[32];
            new byte[] { 0x37, 0x7a, 0xbc, 0xaf, 0x27, 0x1c, 0, 4 }.CopyTo(header, 0);
            BitConverter.GetBytes(64UL * 1024 * 1024).CopyTo(header, 20);
            BitConverter.GetBytes(Crc32(header, 12, 20)).CopyTo(header, 8);
            var path = Path.Combine(root, "malformed.7z");
            File.WriteAllBytes(path, header);
            var service = new MultiFormatArchiveService();
            var error = await Assert.ThrowsAsync<IncompleteArchiveException>(async () =>
            {
                if (extract) await service.ExtractAsync(path, Path.Combine(root, "out"));
                else await service.ListEntriesAsync(path);
            });
            Assert.Contains("Next header extends beyond the end of stream", error.Message);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static uint Crc32(byte[] bytes, int offset, int count)
    {
        var crc = uint.MaxValue;
        for (var index = offset; index < offset + count; index++)
        {
            crc ^= bytes[index];
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0U : 0xedb88320U);
        }
        return ~crc;
    }
}
