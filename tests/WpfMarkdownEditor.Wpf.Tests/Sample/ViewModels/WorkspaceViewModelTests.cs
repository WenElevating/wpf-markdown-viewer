using System.IO;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Sample.ViewModels;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.ViewModels;

public sealed class WorkspaceViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.WorkspaceViewModelTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadCurrentFileDirectoryAsync_LoadsDirectoryAndSelectsCurrentFile()
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, "open.md");
        File.WriteAllText(filePath, "# Open");
        File.WriteAllText(Path.Combine(_directory, "other.md"), "# Other");
        var nestedDirectory = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedFilePath = Path.Combine(nestedDirectory, "deep.md");
        File.WriteAllText(nestedFilePath, "# Deep");
        var viewModel = new WorkspaceViewModel(new FolderWorkspaceService());

        var node = await viewModel.LoadCurrentFileDirectoryAsync(filePath);

        Assert.NotNull(node);
        Assert.Equal(Path.GetFullPath(_directory), viewModel.WorkspaceFolderPath);
        var selected = Assert.Contains(Path.GetFullPath(filePath), viewModel.WorkspaceIndex);
        Assert.False(selected.IsDirectory);
        Assert.True(selected.IsSelected);
        Assert.DoesNotContain(Path.GetFullPath(nestedFilePath), viewModel.WorkspaceIndex.Keys);
    }

    [Fact]
    public async Task LoadNodeChildrenAsync_LoadsExpandedDirectoryOneLevel()
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, "open.md");
        File.WriteAllText(filePath, "# Open");
        var nestedDirectory = Path.Combine(_directory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedFilePath = Path.Combine(nestedDirectory, "deep.md");
        File.WriteAllText(nestedFilePath, "# Deep");
        var viewModel = new WorkspaceViewModel(new FolderWorkspaceService());

        await viewModel.LoadCurrentFileDirectoryAsync(filePath);
        var nested = Assert.Contains(Path.GetFullPath(nestedDirectory), viewModel.WorkspaceIndex);
        Assert.False(nested.ChildrenLoaded);

        await viewModel.LoadNodeChildrenAsync(nested);

        Assert.True(nested.ChildrenLoaded);
        var file = Assert.Contains(Path.GetFullPath(nestedFilePath), viewModel.WorkspaceIndex);
        Assert.False(file.IsDirectory);
    }

    [Fact]
    public async Task RemoveNode_RemovesIndexedNodeAndParentChild()
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, "open.md");
        File.WriteAllText(filePath, "# Open");
        var viewModel = new WorkspaceViewModel(new FolderWorkspaceService());
        await viewModel.LoadCurrentFileDirectoryAsync(filePath);

        viewModel.RemoveNode(filePath);

        Assert.DoesNotContain(Path.GetFullPath(filePath), viewModel.WorkspaceIndex.Keys);
        Assert.DoesNotContain(viewModel.Root!.Children, node => node.FullPath == Path.GetFullPath(filePath));
    }

    [Fact]
    public async Task SelectNode_RaisesSelectingStateDuringSelectionOnly()
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, "open.md");
        File.WriteAllText(filePath, "# Open");
        var viewModel = new WorkspaceViewModel(new FolderWorkspaceService());
        await viewModel.LoadCurrentFileDirectoryAsync(filePath);
        var node = Assert.Contains(Path.GetFullPath(filePath), viewModel.WorkspaceIndex);
        var observed = new List<bool>();
        viewModel.SelectionStateChanged += (_, isSelecting) => observed.Add(isSelecting);

        viewModel.SelectNode(node);

        Assert.Equal([true, false], observed);
        Assert.False(viewModel.IsSelectingNode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
