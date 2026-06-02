using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMarkdownEditor.Core;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Wpf.Rendering;
using WpfMarkdownEditor.Wpf.Theming;
using Xunit;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;

namespace WpfMarkdownEditor.Wpf.Tests.Rendering;

public sealed class HtmlRenderingTests
{
    private static readonly byte[] SvgBadge = Encoding.UTF8.GetBytes(
        """<svg xmlns="http://www.w3.org/2000/svg" width="80" height="20"><rect width="80" height="20" fill="#d73a49"/></svg>""");

    [Fact]
    public void Render_HtmlBlock_DoesNotExposeRawHtmlTags()
    {
        RunOnSta(() =>
        {
            var document = Render(
                """
                <div align="center">
                <h4>Support Latest Version</h4>
                <p><strong>Hello</strong></p>
                </div>
                """);

            var text = GetDocumentText(document);

            Assert.Contains("Support Latest Version", text);
            Assert.Contains("Hello", text);
            Assert.DoesNotContain("<div", text);
            Assert.DoesNotContain("<strong", text);
        });
    }

    [Fact]
    public void Render_InlineHtml_UsesWpfInlineElements()
    {
        RunOnSta(() =>
        {
            var document = Render("Before <strong>bold</strong><br><code>x</code> after");

            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<WpfBlock>()));
            var inlines = paragraph.Inlines.ToList();

            Assert.True(ContainsInline<Bold>(inlines));
            Assert.True(ContainsInline<LineBreak>(inlines));
            Assert.True(ContainsInline<Span>(inlines, span => span.FontFamily.Equals(EditorTheme.Light.CodeFont)));
            Assert.False(ContainsInline<Run>(inlines, run => run.Text.Contains("<strong", StringComparison.Ordinal)));
        });
    }

    [Fact]
    public void Render_DetailsBlock_RendersSummaryAndBody()
    {
        RunOnSta(() =>
        {
            var document = Render(
                """
                <details open>
                <summary><b>Auto Run Script</b></summary>

                Run this script:
                <br>
                Done.
                </details>
                """);

            var text = GetDocumentText(document);

            Assert.Contains("Auto Run Script", text);
            Assert.Contains("Run this script:", text);
            Assert.Contains("Done.", text);
            Assert.DoesNotContain("<details", text);
            Assert.DoesNotContain("<summary", text);
        });
    }

    [Fact]
    public void Render_HtmlParagraphWithMarkdownSvgBadge_RendersImageInsteadOfRawSyntax()
    {
        RunOnSta(() =>
        {
            var resolver = new DeferredImageResolver();
            var document = Render(
                """
                <p align="center">

                ![Release](https://img.shields.io/github/v/release/yeongpin/cursor-free-vip?style=flat-square&logo=github&color=blue)

                </p>
                """,
                resolver);

            var text = GetDocumentText(document);

            Assert.DoesNotContain("![Release]", text);
            Assert.True(WaitUntil(() => resolver.ResolveStarted.Task.IsCompleted));
            Assert.Equal(
                "https://img.shields.io/github/v/release/yeongpin/cursor-free-vip?style=flat-square&logo=github&color=blue",
                resolver.ResolveStarted.Task.Result);

            resolver.Complete(SvgBadge, "svg");

            Assert.True(WaitUntil(() => DocumentContainsElement<WebBrowser>(document)));
        });
    }

    [Fact]
    public void Render_DetailsBlockWithMarkdownFencedCode_RendersCodeBlockInsteadOfRawFence()
    {
        RunOnSta(() =>
        {
            var document = Render(
                """
                <details open>
                <summary><b>Auto Run Script | 脚本自动化运行</b></summary>

                **Linux/macOS**

                ```bash
                curl -fsSL https://raw.githubusercontent.com/yeongpin/cursor-free-vip/main/scripts/install.sh -o install.sh
                ```

                </details>
                """);

            var text = GetDocumentText(document);

            Assert.Contains("Auto Run Script", text);
            Assert.Contains("Linux/macOS", text);
            Assert.Contains("curl -fsSL", text);
            Assert.DoesNotContain("```bash", text);
            Assert.DoesNotContain("**Linux", text);
            Assert.Contains(document.Blocks.Cast<WpfBlock>(), ContainsCodeBlock);
        });
    }

    [Theory]
    [InlineData("header.md", "Support Latest Version")]
    [InlineData("details.md", "Auto Run Script")]
    [InlineData("image-table.md", "coffee")]
    public void Render_Fixture_DoesNotExposeRawHtmlTags(string fixtureName, string expectedText)
    {
        RunOnSta(() =>
        {
            var document = Render(ReadFixture(fixtureName));
            var text = GetDocumentText(document);

            Assert.Contains(expectedText, text);
            Assert.DoesNotContain("<div", text);
            Assert.DoesNotContain("<img", text);
            Assert.DoesNotContain("<table", text);
        });
    }

    [Fact]
    public void Render_HtmlTable_CreatesWpfTable()
    {
        RunOnSta(() =>
        {
            var document = Render(ReadFixture("image-table.md"));

            Assert.Contains(document.Blocks.Cast<WpfBlock>(), ContainsTable);
        });
    }

    private static FlowDocument Render(string markdown, IImageResolver? imageResolver = null)
    {
        var parser = new MarkdownParser();
        var renderer = new FlowDocumentRenderer(EditorTheme.Light, imageResolver);
        return renderer.Render(parser.Parse(markdown));
    }

    private static string GetDocumentText(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text;

    private static string ReadFixture(string fixtureName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "tests",
                "WpfMarkdownEditor.Wpf.Tests",
                "Fixtures",
                "HtmlRendering",
                fixtureName);

            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find HTML rendering fixture '{fixtureName}'.");
    }

    private static bool ContainsTable(WpfBlock block) =>
        block switch
        {
            Table => true,
            Section section => section.Blocks.Cast<WpfBlock>().Any(ContainsTable),
            _ => false
        };

    private static bool ContainsCodeBlock(WpfBlock block) =>
        block switch
        {
            Section section => BrushMatches(section.Background, EditorTheme.Light.CodeBackground) ||
                               section.Blocks.Cast<WpfBlock>().Any(ContainsCodeBlock),
            _ => false
        };

    private static bool BrushMatches(Brush? brush, Color color) =>
        brush is SolidColorBrush solidColorBrush && solidColorBrush.Color == color;

    private static bool DocumentContainsElement<TElement>(FlowDocument document)
        where TElement : UIElement =>
        document.Blocks.Cast<WpfBlock>().Any(BlockContainsElement<TElement>);

    private static bool BlockContainsElement<TElement>(WpfBlock block)
        where TElement : UIElement =>
        block switch
        {
            BlockUIContainer container => container.Child is TElement,
            Paragraph paragraph => paragraph.Inlines.Cast<WpfInline>().Any(InlineContainsElement<TElement>),
            Section section => section.Blocks.Cast<WpfBlock>().Any(BlockContainsElement<TElement>),
            Table table => table.RowGroups
                .SelectMany(static group => group.Rows)
                .SelectMany(static row => row.Cells)
                .Any(cell => cell.Blocks.Cast<WpfBlock>().Any(BlockContainsElement<TElement>)),
            _ => false
        };

    private static bool InlineContainsElement<TElement>(WpfInline inline)
        where TElement : UIElement =>
        inline switch
        {
            InlineUIContainer container => container.Child is TElement,
            Span span => span.Inlines.Cast<WpfInline>().Any(InlineContainsElement<TElement>),
            _ => false
        };

    private static bool ContainsInline<TInline>(
        IEnumerable<WpfInline> inlines,
        Func<TInline, bool>? predicate = null)
        where TInline : WpfInline
    {
        foreach (var inline in inlines)
        {
            if (inline is TInline matched && (predicate is null || predicate(matched)))
                return true;

            if (inline is Span span && ContainsInline(span.Inlines.Cast<WpfInline>(), predicate))
                return true;
        }

        return false;
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

    private sealed class DeferredImageResolver : IImageResolver
    {
        private readonly TaskCompletionSource<ImageData?> _imageData = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<string> ResolveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ImageData?> ResolveImageAsync(string url, CancellationToken ct)
        {
            ResolveStarted.TrySetResult(url);
            return _imageData.Task;
        }

        public void Complete(byte[]? data, string format = "png") =>
            _imageData.TrySetResult(data is null ? null : new ImageData { Data = data, Format = format });
    }
}
