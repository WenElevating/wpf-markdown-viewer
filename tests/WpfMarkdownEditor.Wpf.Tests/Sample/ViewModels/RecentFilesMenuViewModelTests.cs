using System.IO;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Sample.ViewModels;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.ViewModels;

public sealed class RecentFilesMenuViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.RecentFilesMenuViewModelTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RefreshAsync_LoadsSnapshotAndMarksCacheLoaded()
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, "open.md");
        File.WriteAllText(filePath, "# Open");
        var service = new RecentFilesService(_directory, Guid.NewGuid().ToString("N"));
        service.AddOrRefreshFile(filePath);
        var viewModel = new RecentFilesMenuViewModel(service);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsCacheLoaded);
        var entry = Assert.Single(viewModel.Entries);
        Assert.Equal(Path.GetFullPath(filePath), entry.Path);
    }

    [Fact]
    public void AddOrRefresh_PersistsAndMovesPathToFrontAndCapsEntries()
    {
        var mutexName = Guid.NewGuid().ToString("N");
        var viewModel = new RecentFilesMenuViewModel(new RecentFilesService(_directory, mutexName));

        foreach (var index in Enumerable.Range(1, 25))
        {
            var path = Path.Combine(_directory, $"file-{index}.md");
            Directory.CreateDirectory(_directory);
            File.WriteAllText(path, "# File");
            viewModel.AddOrRefresh(path);
        }

        Assert.Equal(20, viewModel.Entries.Count);
        Assert.EndsWith("file-25.md", viewModel.Entries[0].Path);

        var reloaded = new RecentFilesService(_directory, mutexName).LoadFiles();
        Assert.Contains(reloaded, entry => entry.Path.EndsWith("file-25.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatDisplayPath_TruncatesLongPaths()
    {
        var path = Path.Combine(
            _directory,
            "very-long-folder-name-that-should-not-fill-the-recent-files-menu",
            "open.md");

        var display = RecentFilesMenuViewModel.FormatDisplayPath(path);

        Assert.Equal($"{Path.GetPathRoot(path)}...\\open.md", display);
    }

    [Fact]
    public void CompleteHoverDelay_ClearsCurrentTokenSoLaterCancelDoesNotThrow()
    {
        var viewModel = new RecentFilesMenuViewModel(new RecentFilesService(_directory, Guid.NewGuid().ToString("N")));
        var cts = viewModel.StartHoverDelay();

        viewModel.CompleteHoverDelay(cts);

        var exception = Record.Exception(viewModel.CancelHoverDelay);
        Assert.Null(exception);
    }

    [Fact]
    public void CompleteCloseDelay_ClearsCurrentTokenSoLaterCancelDoesNotThrow()
    {
        var viewModel = new RecentFilesMenuViewModel(new RecentFilesService(_directory, Guid.NewGuid().ToString("N")));
        var cts = viewModel.StartCloseDelay();

        viewModel.CompleteCloseDelay(cts);

        var exception = Record.Exception(viewModel.CancelCloseDelay);
        Assert.Null(exception);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
