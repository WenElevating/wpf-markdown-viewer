using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using MarkItDown.Core;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Wpf.Rendering;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Converters;

public sealed class MarkdownToFlowDocumentConverter : BaseConverter
{
    public override IReadOnlySet<string> SupportedExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };

    public override IReadOnlySet<string> SupportedMimeTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "text/markdown", "text/x-markdown" };

    private readonly EditorTheme _theme;
    private readonly MarkdownParser _parser;

    public MarkdownToFlowDocumentConverter() : this(EditorTheme.Light) { }

    public MarkdownToFlowDocumentConverter(EditorTheme theme)
    {
        _theme = theme;
        _parser = new MarkdownParser();
    }

    public override async Task<DocumentConversionResult> ConvertAsync(
        DocumentConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        var markdown = await ReadMarkdownAsync(request, cancellationToken);
        var xaml = ConvertToXaml(markdown, request.FilePath);
        return new DocumentConversionResult("FlowDocument", xaml);
    }

    public FlowDocument ConvertToFlowDocument(string markdown) =>
        ConvertToFlowDocument(markdown, filePath: null);

    public FlowDocument ConvertToFlowDocument(string markdown, string? filePath)
    {
        var blocks = _parser.Parse(markdown);
        var baseDirectory = GetBaseDirectory(filePath);
        var renderer = baseDirectory is null
            ? new FlowDocumentRenderer(_theme)
            : new FlowDocumentRenderer(_theme, new ImageLoader(baseDirectory));
        return renderer.Render(blocks);
    }

    public string ConvertToXaml(string markdown) =>
        ConvertToXaml(markdown, filePath: null);

    public string ConvertToXaml(string markdown, string? filePath)
    {
        var document = ConvertToFlowDocument(markdown, filePath);
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        XamlWriter.Save(document, writer);
        return sb.ToString();
    }

    private static string? GetBaseDirectory(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(filePath));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    private static async Task<string> ReadMarkdownAsync(
        DocumentConversionRequest request,
        CancellationToken ct)
    {
        if (request.Stream is not null)
        {
            using var reader = new StreamReader(request.Stream);
            return await reader.ReadToEndAsync(ct);
        }

        if (request.FilePath is not null)
        {
            return await File.ReadAllTextAsync(request.FilePath, ct);
        }

        throw new ConversionException("Stream or FilePath is required for Markdown conversion.");
    }
}
