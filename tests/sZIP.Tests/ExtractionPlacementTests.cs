using sZIP.Application;

namespace sZIP.Tests;

public sealed class ExtractionPlacementTests
{
    [Fact]
    public void SmartExtractionKeepsSingleRootFolderWithoutDuplicateArchiveFolder()
    {
        using var sandbox = new TemporaryDirectory();
        var temporary = Directory.CreateDirectory(Path.Combine(sandbox.Path, ".work")).FullName;
        var root = Directory.CreateDirectory(Path.Combine(temporary, "project")).FullName;
        File.WriteAllText(Path.Combine(root, "readme.txt"), "content");

        var output = ExtractionPlacement.Complete(
            temporary, Path.Combine(sandbox.Path, "download.zip"), sandbox.Path, smart: true);

        Assert.Equal(Path.Combine(sandbox.Path, "project"), output);
        Assert.True(File.Exists(Path.Combine(output, "readme.txt")));
        Assert.False(Directory.Exists(temporary));
    }

    [Fact]
    public void SmartExtractionCreatesArchiveNamedFolderForMixedRoots()
    {
        using var sandbox = new TemporaryDirectory();
        var temporary = Directory.CreateDirectory(Path.Combine(sandbox.Path, ".work")).FullName;
        File.WriteAllText(Path.Combine(temporary, "one.txt"), "1");
        File.WriteAllText(Path.Combine(temporary, "two.txt"), "2");

        var output = ExtractionPlacement.Complete(
            temporary, Path.Combine(sandbox.Path, "bundle.tar.gz"), sandbox.Path, smart: true);

        Assert.Equal(Path.Combine(sandbox.Path, "bundle"), output);
        Assert.True(File.Exists(Path.Combine(output, "one.txt")));
        Assert.True(File.Exists(Path.Combine(output, "two.txt")));
    }

    [Fact]
    public void DirectExtractionMovesChildrenAndKeepsExistingFiles()
    {
        using var sandbox = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(sandbox.Path, "readme.txt"), "existing");
        var temporary = Directory.CreateDirectory(Path.Combine(sandbox.Path, ".work")).FullName;
        File.WriteAllText(Path.Combine(temporary, "readme.txt"), "new");

        var output = ExtractionPlacement.Complete(
            temporary, Path.Combine(sandbox.Path, "bundle.zip"), sandbox.Path, smart: false);

        Assert.Equal(sandbox.Path, output);
        Assert.Equal("existing", File.ReadAllText(Path.Combine(sandbox.Path, "readme.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(sandbox.Path, "readme (1).txt")));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "sZIP-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
