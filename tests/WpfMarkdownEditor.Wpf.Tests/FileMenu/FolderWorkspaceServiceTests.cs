using System.IO;
using WpfMarkdownEditor.Sample;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class FolderWorkspaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.FolderWorkspaceTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanAsync_ReturnsMarkdownTreeAndSkipsBuildFolders()
    {
        CreateFile("docs\\b.md");
        CreateFile("docs\\a.markdown");
        CreateFile("docs\\ignore.txt");
        CreateFile(".git\\config.md");
        CreateFile("bin\\debug.md");
        CreateFile("obj\\generated.md");
        var service = new FolderWorkspaceService();

        var result = await service.ScanAsync(_root);

        Assert.False(result.IsTruncated);
        var docs = Assert.Single(result.Root.Children);
        Assert.Equal("docs", docs.Name);
        Assert.Equal(new[] { "a.markdown", "b.md" }, docs.Children.Select(node => node.Name).ToArray());
    }

    [Fact]
    public async Task ScanAsync_TruncatesAtConfiguredMarkdownFileLimit()
    {
        for (var index = 0; index < 5; index++)
            CreateFile($"file-{index}.md");
        var service = new FolderWorkspaceService(new FolderScanOptions(MaxMarkdownFiles: 3, MaxEntries: 50, MaxDepth: 12));

        var result = await service.ScanAsync(_root);

        Assert.True(result.IsTruncated);
        Assert.Equal(3, result.MarkdownFileCount);
        Assert.Equal(3, result.Root.Children.Count);
    }

    [Fact]
    public async Task ScanAsync_RespectsDepthLimit()
    {
        CreateFile("one\\two\\three\\deep.md");
        var service = new FolderWorkspaceService(new FolderScanOptions(MaxMarkdownFiles: 10, MaxEntries: 50, MaxDepth: 2));

        var result = await service.ScanAsync(_root);

        Assert.True(result.IsTruncated);
        var one = Assert.Single(result.Root.Children);
        var two = Assert.Single(one.Children);
        Assert.Empty(two.Children);
    }

    [Fact]
    public async Task ScanShallowAsync_ReturnsDirectoriesWithoutPreScanningAndSkipsDotDirectories()
    {
        CreateFile("root.md");
        CreateFile("LICENSE");
        CreateFile("nested\\deep.md");
        CreateFile("deepOnly\\child\\deep.md");
        CreateFile("empty\\ignore.txt");
        CreateFile(".github\\README.md");
        CreateFile(".hidden\\hidden.md");
        CreateFile("plain.txt");
        var service = new FolderWorkspaceService();

        var result = await service.ScanShallowAsync(_root);

        Assert.False(result.IsTruncated);
        Assert.Equal(2, result.MarkdownFileCount);
        Assert.Equal(
            new[] { "deepOnly", "empty", "nested", "LICENSE", "root.md" },
            result.Root.Children.Select(node => node.Name).ToArray());

        var nested = Assert.Single(result.Root.Children.Where(node => node.Name == "nested"));
        Assert.True(nested.IsDirectory);
        Assert.Empty(nested.Children);
        Assert.False(nested.ChildrenLoaded);

        Assert.DoesNotContain(result.Root.Children, node => node.Name.StartsWith(".", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Root.Children, node => node.Name == "plain.txt");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void CreateFile(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# Test");
    }
}
