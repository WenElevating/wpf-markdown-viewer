using System.IO;
using System.Text;
using WpfMarkdownEditor.Sample.Services;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.Services;

public sealed class DocumentSessionServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.DocumentSessionServiceTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ReadMarkdownAsync_ReadsFileContents()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "open.md");
        await File.WriteAllTextAsync(path, "# Open", Encoding.UTF8);
        var service = new DocumentSessionService();

        var markdown = await service.ReadMarkdownAsync(path);

        Assert.Equal("# Open", markdown);
    }

    [Fact]
    public async Task WriteMarkdownAsync_CreatesDirectoryAndWritesUtf8WithoutBom()
    {
        var path = Path.Combine(_directory, "nested", "save.md");
        var service = new DocumentSessionService();

        await service.WriteMarkdownAsync(path, "# Save");

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal("# Save", Encoding.UTF8.GetString(bytes));
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
