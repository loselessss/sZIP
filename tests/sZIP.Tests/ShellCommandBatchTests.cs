using sZIP.Application;

namespace sZIP.Tests;

public sealed class ShellCommandBatchTests
{
    [Fact]
    public void Create_MergesMultipleCompressInvocationsAndRemovesDuplicatePaths()
    {
        var batch = ShellCommandBatch.Create(new IReadOnlyList<string>[]
        {
            new[] { "--compress", @"C:\자료\첫째.txt" },
            new[] { "--compress", @"C:\자료\둘째.txt" },
            new[] { "--compress", @"c:\자료\첫째.txt" }
        });

        Assert.Equal(2, batch.CompressionPaths.Count);
        Assert.Equal(@"C:\자료\첫째.txt", batch.CompressionPaths[0]);
        Assert.Equal(@"C:\자료\둘째.txt", batch.CompressionPaths[1]);
        Assert.Empty(batch.OtherCommands);
        Assert.Empty(batch.ExtractionPaths);
    }

    [Fact]
    public void Create_KeepsNonCompressionCommandsSeparate()
    {
        var open = new[] { "--open", @"C:\자료\archive.zip" };

        var batch = ShellCommandBatch.Create(new IReadOnlyList<string>[] { open });

        Assert.Empty(batch.CompressionPaths);
        Assert.Empty(batch.ExtractionPaths);
        Assert.Equal(open, Assert.Single(batch.OtherCommands));
    }

    [Fact]
    public void Create_MergesMultipleExtractInvocations()
    {
        var batch = ShellCommandBatch.Create(new IReadOnlyList<string>[]
        {
            new[] { "--extract", @"C:\자료\first.zip" },
            new[] { "--extract", @"C:\자료\second.7z" },
            new[] { "--extract", @"c:\자료\first.zip" }
        });

        Assert.Equal(2, batch.ExtractionPaths.Count);
        Assert.Equal(@"C:\자료\first.zip", batch.ExtractionPaths[0]);
        Assert.Equal(@"C:\자료\second.7z", batch.ExtractionPaths[1]);
        Assert.Empty(batch.CompressionPaths);
        Assert.Empty(batch.OtherCommands);
    }
}
