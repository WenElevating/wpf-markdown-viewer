using System.IO;
using System.Threading;
using System.Windows.Documents;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Wpf.Rendering;
using WpfMarkdownEditor.Wpf.Theming;
using Xunit;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;

namespace WpfMarkdownEditor.Wpf.Tests.Rendering;

public sealed class HtmlRenderingTests
{
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

    private static FlowDocument Render(string markdown)
    {
        var parser = new MarkdownParser();
        var renderer = new FlowDocumentRenderer(EditorTheme.Light);
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
}
