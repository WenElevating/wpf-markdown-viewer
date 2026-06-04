using System.IO;
using System.Text;

namespace WpfMarkdownEditor.Sample.Services;

public sealed class DocumentSessionService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<string> ReadMarkdownAsync(string path, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(path, Utf8NoBom, cancellationToken);
    }

    public async Task WriteMarkdownAsync(string path, string markdown, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, markdown, Utf8NoBom, cancellationToken);
    }

    public void WriteMarkdown(string path, string markdown)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, markdown, Utf8NoBom);
    }
}
