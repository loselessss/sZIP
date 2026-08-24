using sZIP.Application;

namespace sZIP.Tests;

public sealed class ShellCommandBatchTests
{
    [Fact]
    public void Create_MergesMultipleCompressInvocationsAndRemovesDuplicatePaths()
    {
        var batch = ShellCommandBatch.Create(new IReadOnlyList<string>[]
        {
            new[] { "--compress", @"C:\Cafe\first.txt" },
            new[] { "--compress", @"C:\Cafe\second.txt" },
            new[] { "--compress", @"c:\Cafe\first.txt" }
        });

        Assert.Equal(2, batch.CompressionPaths.Count);
        Assert.Equal(@"C:\Cafe\first.txt", batch.CompressionPaths[0]);
        Assert.Equal(@"C:\Cafe\second.txt", batch.CompressionPaths[1]);
        Assert.Empty(batch.OtherCommands);
        Assert.Empty(batch.DirectExtractionPaths);
        Assert.Empty(batch.SmartExtractionPaths);
    }

    [Fact]
    public void Create_KeepsNonCompressionCommandsSeparate()
    {
        var open = new[] { "--open", @"C:\Cafe\archive.zip" };

        var batch = ShellCommandBatch.Create(new IReadOnlyList<string>[] { open });

        Assert.Empty(batch.CompressionPaths);
        Assert.Empty(batch.DirectExtractionPaths);
        Assert.Empty(batch.SmartExtractionPaths);
        Assert.Equal(open, Assert.Single(batch.OtherCommands));
    }

    [Fact]
    public void Create_MergesDirectAndSmartExtractInvocationsSeparately()
    {
        var batch = ShellCommandBatch.Create(new IReadOnlyList<string>[]
        {
            new[] { "--extract-direct", @"C:\Cafe\first.zip" },
            new[] { "--extract-direct", @"c:\Cafe\first.zip" },
            new[] { "--extract-smart", @"C:\Cafe\second.7z" },
            new[] { "--extract", @"C:\Cafe\legacy.rar" }
        });

        Assert.Equal(new[] { @"C:\Cafe\first.zip" }, batch.DirectExtractionPaths);
        Assert.Equal(
            new[] { @"C:\Cafe\second.7z", @"C:\Cafe\legacy.rar" },
            batch.SmartExtractionPaths);
        Assert.Empty(batch.CompressionPaths);
        Assert.Empty(batch.OtherCommands);
    }
}
