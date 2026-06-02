using System.IO;
using WpfMarkdownEditor.Sample;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class RecentFilesServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.RecentFilesTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddOrRefreshFile_StoresMostRecentUniqueExistingPaths()
    {
        Directory.CreateDirectory(_directory);
        var service = new RecentFilesService(_directory, "RecentFilesServiceTests.Unique");
        var first = CreateFile("first.md");
        var second = CreateFile("second.md");

        service.AddOrRefreshFile(first);
        service.AddOrRefreshFile(second);
        service.AddOrRefreshFile(first);

        var files = service.LoadFiles();

        Assert.Equal(new[] { first, second }, files.Select(file => file.Path).ToArray());
    }

    [Fact]
    public void AddOrRefreshFile_CapsListAtTwenty()
    {
        Directory.CreateDirectory(_directory);
        var service = new RecentFilesService(_directory, "RecentFilesServiceTests.Cap");

        for (var index = 0; index < 25; index++)
            service.AddOrRefreshFile(CreateFile($"note-{index:00}.md"));

        var files = service.LoadFiles();

        Assert.Equal(20, files.Count);
        Assert.EndsWith("note-24.md", files[0].Path);
        Assert.EndsWith("note-05.md", files[^1].Path);
    }

    [Fact]
    public void LoadFiles_RemovesMissingFilesAndPersistsCleanList()
    {
        Directory.CreateDirectory(_directory);
        var service = new RecentFilesService(_directory, "RecentFilesServiceTests.Missing");
        var existing = CreateFile("existing.md");
        var missing = CreateFile("missing.md");
        service.AddOrRefreshFile(existing);
        service.AddOrRefreshFile(missing);
        File.Delete(missing);

        var files = service.LoadFiles(removeMissingFiles: true);

        Assert.Single(files);
        Assert.Equal(existing, files[0].Path);
        Assert.DoesNotContain("missing.md", File.ReadAllText(Path.Combine(_directory, "recent-files.json")));
    }

    [Fact]
    public void LoadFiles_ReturnsEmptyForMalformedJson()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "recent-files.json"), "{ not-json");
        var service = new RecentFilesService(_directory, "RecentFilesServiceTests.Malformed");

        Assert.Empty(service.LoadFiles());
    }

    [Fact]
    public void ClearFiles_RemovesPersistedEntries()
    {
        Directory.CreateDirectory(_directory);
        var service = new RecentFilesService(_directory, "RecentFilesServiceTests.Clear");
        service.AddOrRefreshFile(CreateFile("first.md"));
        service.AddOrRefreshFile(CreateFile("second.md"));

        service.ClearFiles();

        Assert.Empty(service.LoadFiles());
    }

    [Fact]
    public async Task LoadFilesAsync_ReturnsPersistedEntries()
    {
        Directory.CreateDirectory(_directory);
        var service = new RecentFilesService(_directory, "RecentFilesServiceTests.LoadAsync");
        var first = CreateFile("first.md");

        service.AddOrRefreshFile(first);

        var files = await service.LoadFilesAsync(removeMissingFiles: true);

        Assert.Single(files);
        Assert.Equal(first, files[0].Path);
    }

    [Fact]
    public async Task AddOrRefreshFile_SerializesConcurrentWrites()
    {
        Directory.CreateDirectory(_directory);
        var first = CreateFile("first.md");
        var second = CreateFile("second.md");
        var firstService = new RecentFilesService(_directory, "RecentFilesServiceTests.Concurrent");
        var secondService = new RecentFilesService(_directory, "RecentFilesServiceTests.Concurrent");

        await Task.WhenAll(
            Task.Run(() => firstService.AddOrRefreshFile(first)),
            Task.Run(() => secondService.AddOrRefreshFile(second)));

        var paths = firstService.LoadFiles().Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(first, paths);
        Assert.Contains(second, paths);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, "# Test");
        return path;
    }
}
