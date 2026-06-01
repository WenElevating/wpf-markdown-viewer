using System.IO;
using WpfMarkdownEditor.Sample;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class FileOperationServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.FileOperationTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void MoveFile_MovesAndAllowsOverwriteOnlyWhenRequested()
    {
        Directory.CreateDirectory(_directory);
        var source = WriteFile("source.md", "source");
        var destination = WriteFile("destination.md", "destination");
        var service = new FileOperationService();

        Assert.Throws<IOException>(() => service.MoveFile(source, destination, overwrite: false));

        service.MoveFile(source, destination, overwrite: true);

        Assert.False(File.Exists(source));
        Assert.Equal("source", File.ReadAllText(destination));
    }

    [Fact]
    public void DeleteFile_DeletesExistingFile()
    {
        Directory.CreateDirectory(_directory);
        var path = WriteFile("delete.md", "content");
        var service = new FileOperationService();

        service.DeleteFile(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void GetProperties_ReturnsFileMetadata()
    {
        Directory.CreateDirectory(_directory);
        var path = WriteFile("props.md", "12345");
        var service = new FileOperationService();

        var properties = service.GetProperties(path);

        Assert.Equal(path, properties.Path);
        Assert.Equal(5, properties.SizeBytes);
        Assert.True(properties.LastModifiedUtc <= DateTime.UtcNow);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
