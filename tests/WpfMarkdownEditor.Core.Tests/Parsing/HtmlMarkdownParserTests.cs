using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Html;
using WpfMarkdownEditor.Core.Parsing.Inlines;
using Xunit;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public sealed class HtmlMarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void Parse_BlockHtmlAtBlockStart_ReturnsHtmlBlock()
    {
        var blocks = _parser.Parse(
            """
            <div align="center">

            <img src="logo.png" alt="Logo" />

            </div>
            """);

        var block = Assert.IsType<HtmlBlock>(Assert.Single(blocks));
        Assert.Equal(HtmlFragmentKind.Block, block.Fragment.Kind);
        var div = Assert.IsType<HtmlElementNode>(Assert.Single(block.Fragment.Children));
        Assert.Equal("div", div.TagName);
        Assert.Equal("center", div.Attributes["align"]);
        Assert.Contains(div.Children, node => node is HtmlElementNode { TagName: "img" });
    }

    [Fact]
    public void Parse_BlankLinesInsideUnclosedHtmlBlock_DoNotTerminateFragment()
    {
        var blocks = _parser.Parse(
            """
            <div align="center">

            <img src="logo.png" />

            </div>
            """);

        var block = Assert.IsType<HtmlBlock>(Assert.Single(blocks));
        Assert.Equal(5, block.LineEnd);
    }

    [Fact]
    public void Parse_SingleLineClosedHtmlBlock_DoesNotConsumeFollowingParagraph()
    {
        var blocks = _parser.Parse(
            """
            <div>logo</div>
            Next paragraph
            """);

        Assert.Collection(
            blocks,
            block => Assert.IsType<HtmlBlock>(block),
            block =>
            {
                var paragraph = Assert.IsType<ParagraphBlock>(block);
                var text = Assert.IsType<TextInline>(Assert.Single(paragraph.Inlines));
                Assert.Equal("Next paragraph", text.Content);
            });
    }

    [Fact]
    public void Parse_SingleLineClosedHtmlBlock_DoesNotConsumeFollowingImage()
    {
        var blocks = _parser.Parse(
            """
            <h1 align="center">Title</h1>
            ![clipboard](images/clipboard.png)
            """);

        Assert.Collection(
            blocks,
            block => Assert.IsType<HtmlBlock>(block),
            block =>
            {
                var paragraph = Assert.IsType<ParagraphBlock>(block);
                var image = Assert.IsType<ImageInline>(Assert.Single(paragraph.Inlines));
                Assert.Equal("images/clipboard.png", image.Url);
            });
    }

    [Fact]
    public void Parse_MixedLineBlockTag_DoesNotPromoteToHtmlBlock()
    {
        var blocks = _parser.Parse("""Some text <div align="center">logo</div> more text""");

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
        var text = Assert.IsType<TextInline>(Assert.Single(paragraph.Inlines));
        Assert.Equal("""Some text <div align="center">logo</div> more text""", text.Content);
    }

    [Fact]
    public void ParseInlines_SupportedInlineHtml_ReturnsHtmlInline()
    {
        var result = _parser.ParseInlines("<strong>bold</strong><br><code>x</code>");

        Assert.Collection(
            result,
            inline => Assert.IsType<HtmlInline>(inline),
            inline => Assert.IsType<HtmlInline>(inline),
            inline => Assert.IsType<HtmlInline>(inline));
    }

    [Fact]
    public void ParseInlines_SingleHtmlImage_ReturnsImageInline()
    {
        var result = _parser.ParseInlines("""<img src="logo.png" alt='Logo' title=Preview>""");

        var image = Assert.IsType<ImageInline>(Assert.Single(result));
        Assert.Equal("logo.png", image.Url);
        Assert.Equal("Logo", image.Alt);
        Assert.Equal("Preview", image.Title);
    }

    [Fact]
    public void ParseInlines_SingleHtmlImageWithStyleDimensions_ReturnsDisplaySize()
    {
        var result = _parser.ParseInlines("""<img src="logo.svg" style="width: 250px; height: 55px;" width="80" height="20">""");

        var image = Assert.IsType<ImageInline>(Assert.Single(result));
        Assert.Equal("logo.svg", image.Url);
        Assert.Equal(250, image.DisplayWidth);
        Assert.Equal(55, image.DisplayHeight);
    }

    [Fact]
    public void ParseInlines_AnchorWrappedHtmlImage_ReturnsHtmlInline()
    {
        var result = _parser.ParseInlines(
            """<a href="https://example.com"><img src="logo.png" alt="Logo"></a>""");

        var html = Assert.IsType<HtmlInline>(Assert.Single(result));
        var anchor = Assert.IsType<HtmlElementNode>(Assert.Single(html.Fragment.Children));
        Assert.Equal("a", anchor.TagName);
        Assert.Contains(anchor.Children, node => node is HtmlElementNode { TagName: "img" });
    }

    [Fact]
    public void ParseInlines_AnchorWrappedPictureImage_ReturnsHtmlInline()
    {
        var result = _parser.ParseInlines(
            """
            <a href="https://www.star-history.com/?repos=Lum1104%2FUnderstand-Anything&type=date&legend=top-left"><picture><source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/image?repos=Lum1104/Understand-Anything&type=date&theme=dark&legend=top-left" /><img alt="Star History Chart" src="https://api.star-history.com/image?repos=Lum1104/Understand-Anything&type=date&legend=top-left" /></picture></a>
            """);

        var html = Assert.IsType<HtmlInline>(Assert.Single(result));
        var anchor = Assert.IsType<HtmlElementNode>(Assert.Single(html.Fragment.Children));
        var picture = Assert.IsType<HtmlElementNode>(Assert.Single(anchor.Children));

        Assert.Equal("picture", picture.TagName);
        Assert.Contains(picture.Children, node => node is HtmlElementNode { TagName: "source" });
        Assert.Contains(picture.Children, node => node is HtmlElementNode { TagName: "img" });
    }

    [Fact]
    public void Parse_ConsecutiveAnchorWrappedHtmlImages_PreservesLineBreakBetweenHtmlInlines()
    {
        var blocks = _parser.Parse(
            """
            <a href="https://996.icu"><img src="https://img.shields.io/badge/link-996.icu-red.svg"></a>
            <a href="https://996.icu"><img src="https://camo.githubusercontent.com/image"></a>
            """);

        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
        Assert.IsType<HtmlInline>(paragraph.Inlines[0]);
        Assert.IsType<LineBreakInline>(paragraph.Inlines[1]);
        Assert.IsType<HtmlInline>(paragraph.Inlines[2]);
    }
}
