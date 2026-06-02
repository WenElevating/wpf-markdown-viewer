using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using MarkItDown.Core;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Converters.Tests;

public class MarkdownToFlowDocumentConverterTests
{
    private static readonly byte[] Png1x1 =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

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
    public void ConvertToFlowDocument_WithFilePath_UsesFileDirectoryForImages()
    {
        RunOnSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "WpfMarkdownEditor.Converter.Tests", Guid.NewGuid().ToString("N"));
            var imageDirectory = Path.Combine(root, "images");
            var markdownPath = Path.Combine(root, "readme.md");
            Directory.CreateDirectory(imageDirectory);
            File.WriteAllText(markdownPath, "![logo](images/logo.png)");
            File.WriteAllBytes(Path.Combine(imageDirectory, "logo.png"), Png1x1);

            try
            {
                var document = _converter.ConvertToFlowDocument(File.ReadAllText(markdownPath), markdownPath);

                Assert.True(WaitUntil(() => DocumentContainsImage(document)));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });
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

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;

            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(10);
        }

        return condition();
    }

    private static bool DocumentContainsImage(FlowDocument document) =>
        document.Blocks.Cast<Block>().Any(BlockContainsImage);

    private static bool BlockContainsImage(Block block) =>
        block switch
        {
            BlockUIContainer container => container.Child is Image image && image.Source is not null ||
                                          container.Child is ContentControl host && host.Content is Image hostImage && hostImage.Source is not null,
            Section section => section.Blocks.Cast<Block>().Any(BlockContainsImage),
            Paragraph paragraph => paragraph.Inlines.Cast<Inline>().Any(InlineContainsImage),
            Table table => table.RowGroups
                .SelectMany(group => group.Rows)
                .SelectMany(row => row.Cells)
                .SelectMany(cell => cell.Blocks.Cast<Block>())
                .Any(BlockContainsImage),
            _ => false
        };

    private static bool InlineContainsImage(Inline inline) =>
        inline switch
        {
            InlineUIContainer container => container.Child is Image image && image.Source is not null,
            Span span => span.Inlines.Cast<Inline>().Any(InlineContainsImage),
            _ => false
        };
}
