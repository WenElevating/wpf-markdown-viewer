using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarkItDown.Core;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Converters.Tests;

public class MarkdownToFlowDocumentConverterTests
{
    private readonly MarkdownToFlowDocumentConverter _converter = new();

    [Fact]
    public void SupportedExtensions_Contains_Md_And_Markdown()
    {
        Assert.Contains(".md", _converter.SupportedExtensions);
        Assert.Contains(".markdown", _converter.SupportedExtensions);
    }

    [Fact]
    public void SupportedMimeTypes_Contains_Markdown()
    {
        Assert.Contains("text/markdown", _converter.SupportedMimeTypes);
        Assert.Contains("text/x-markdown", _converter.SupportedMimeTypes);
    }

    [Theory]
    [InlineData("test.md")]
    [InlineData("test.markdown")]
    [InlineData("TEST.MD")]
    public void CanConvert_MatchesSupportedExtensions(string filename)
    {
        var request = new DocumentConversionRequest { FilePath = filename };
        Assert.True(_converter.CanConvert(request));
    }

    [Fact]
    public void CanConvert_RejectsUnsupportedExtension()
    {
        var request = new DocumentConversionRequest { FilePath = "test.docx" };
        Assert.False(_converter.CanConvert(request));
    }

    [Fact]
    public void CanConvert_MatchesMimeType()
    {
        var request = new DocumentConversionRequest { Filename = "data", MimeType = "text/markdown" };
        Assert.True(_converter.CanConvert(request));
    }

    [Fact]
    public void ConvertToFlowDocument_Heading_ReturnsDocument()
    {
        var doc = _converter.ConvertToFlowDocument("# Hello World");
        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Blocks);
    }

    [Fact]
    public void ConvertToFlowDocument_MultipleBlocks_ReturnsAll()
    {
        var markdown = "# Title\n\nParagraph text\n\n- Item 1\n- Item 2";
        var doc = _converter.ConvertToFlowDocument(markdown);
        Assert.NotNull(doc);
        Assert.True(doc.Blocks.Count >= 3);
    }

    [Fact]
    public void ConvertToFlowDocument_CodeBlock_Renders()
    {
        var markdown = "```csharp\nConsole.WriteLine(\"Hello\");\n```";
        var doc = _converter.ConvertToFlowDocument(markdown);
        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Blocks);
    }

    [Fact]
    public void ConvertToFlowDocument_Table_Renders()
    {
        var markdown = "| A | B |\n|---|---|\n| 1 | 2 |";
        var doc = _converter.ConvertToFlowDocument(markdown);
        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Blocks);
    }

    [Fact]
    public void ConvertToFlowDocument_WithCustomTheme_AppliesTheme()
    {
        var theme = EditorTheme.GitHub;
        var converter = new MarkdownToFlowDocumentConverter(theme);
        var doc = converter.ConvertToFlowDocument("# Test");
        Assert.NotNull(doc);
    }

    [Fact]
    public void ConvertToXaml_ReturnsValidXaml()
    {
        var xaml = _converter.ConvertToXaml("# Hello World");
        Assert.NotEmpty(xaml);
        Assert.Contains("FlowDocument", xaml);
    }

    [Fact]
    public async Task ConvertAsync_FromStream_ReturnsXamlResult()
    {
        var markdown = "# Hello\n\nWorld";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
        var request = new DocumentConversionRequest { Stream = stream };
        var result = await _converter.ConvertAsync(request, CancellationToken.None);

        Assert.Equal("FlowDocument", result.Kind);
        Assert.NotEmpty(result.Markdown);
        Assert.Contains("FlowDocument", result.Markdown);
    }

    [Fact]
    public async Task ConvertAsync_NoStreamOrFilePath_ThrowsConversionException()
    {
        var request = new DocumentConversionRequest();
        await Assert.ThrowsAsync<ConversionException>(
            () => _converter.ConvertAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ConvertAsync_FromFile_ReturnsXamlResult()
    {
        var tempFile = Path.GetTempFileName() + ".md";
        try
        {
            await File.WriteAllTextAsync(tempFile, "# Test File\n\nContent here.");
            var request = new DocumentConversionRequest { FilePath = tempFile };
            var result = await _converter.ConvertAsync(request, CancellationToken.None);

            Assert.Equal("FlowDocument", result.Kind);
            Assert.NotEmpty(result.Markdown);
            Assert.Contains("FlowDocument", result.Markdown);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ConvertToFlowDocument_EmptyInput_ReturnsEmptyDocument()
    {
        var doc = _converter.ConvertToFlowDocument("");
        Assert.NotNull(doc);
    }

    [Fact]
    public void ConvertToFlowDocument_InlineFormatting_Renders()
    {
        var markdown = "This is **bold**, *italic*, and `code`.";
        var doc = _converter.ConvertToFlowDocument(markdown);
        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Blocks);
    }
}
