using Xunit;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Inlines;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        var result = _parser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmptyList()
    {
        var result = _parser.Parse(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PlainText_ReturnsParagraph()
    {
        var result = _parser.Parse("Hello world");
        var block = Assert.Single(result);
        var para = Assert.IsType<ParagraphBlock>(block);
        var text = Assert.IsType<TextInline>(Assert.Single(para.Inlines));
        Assert.Equal("Hello world", text.Content);
    }

    #region ATX Headings

    [Theory]
    [InlineData("# H1", 1)]
    [InlineData("## H2", 2)]
    [InlineData("### H3", 3)]
    [InlineData("#### H4", 4)]
    [InlineData("##### H5", 5)]
    [InlineData("###### H6", 6)]
    public void Parse_AtxHeading_ReturnsCorrectLevel(string input, int expectedLevel)
    {
        var result = _parser.Parse(input);
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(expectedLevel, heading.Level);
        var text = Assert.IsType<TextInline>(Assert.Single(heading.Inlines));
        Assert.Equal($"H{expectedLevel}", text.Content);
    }

    [Fact]
    public void Parse_AtxHeading_TrailingHashesStripped()
    {
        var result = _parser.Parse("# Hello ##");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        var text = Assert.IsType<TextInline>(Assert.Single(heading.Inlines));
        Assert.Equal("Hello", text.Content);
    }

    #endregion

    #region Paragraph

    [Fact]
    public void Parse_MultiLineParagraph_ReturnsSingleParagraph()
    {
        var result = _parser.Parse("Line one\nLine two");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(result));
        var text = Assert.IsType<TextInline>(Assert.Single(para.Inlines));
        Assert.Equal("Line one Line two", text.Content);
    }

    #endregion

    #region Code Blocks

    [Fact]
    public void Parse_FencedCodeBlock_Backticks()
    {
        var result = _parser.Parse("```\ncode\n```");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("code", code.Code);
        Assert.Null(code.Language);
    }

    [Fact]
    public void Parse_FencedCodeBlock_WithLanguage()
    {
        var result = _parser.Parse("```csharp\nvar x = 1;\n```");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("csharp", code.Language);
        Assert.Equal("var x = 1;", code.Code);
    }

    [Fact]
    public void Parse_FencedCodeBlock_Tildes()
    {
        var result = _parser.Parse("~~~\ncode\n~~~");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("code", code.Code);
    }

    [Fact]
    public void Parse_IndentedCodeBlock()
    {
        var result = _parser.Parse("    code line");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("code line", code.Code);
    }

    #endregion

    #region Thematic Break

    [Theory]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    [InlineData("- - -")]
    [InlineData("* * *")]
    public void Parse_ThematicBreak_ReturnsThematicBreakBlock(string input)
    {
        var result = _parser.Parse(input);
        Assert.IsType<ThematicBreakBlock>(Assert.Single(result));
    }

    #endregion

    #region Blockquote

    [Fact]
    public void Parse_Blockquote_ReturnsBlockquoteWithContent()
    {
        var result = _parser.Parse("> Hello");
        var bq = Assert.IsType<BlockquoteBlock>(Assert.Single(result));
        Assert.NotEmpty(bq.Children);
    }

    #endregion

    #region Lists

    [Fact]
    public void Parse_UnorderedList_ReturnsListBlock()
    {
        var result = _parser.Parse("- Item 1\n- Item 2");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_OrderedList_ReturnsListBlock()
    {
        var result = _parser.Parse("1. First\n2. Second");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.True(list.IsOrdered);
        Assert.Equal(2, list.Items.Count);
    }

    #endregion

    #region Inline Elements

    [Fact]
    public void ParseInlines_Bold()
    {
        var result = _parser.ParseInlines("**bold**");
        var bold = Assert.IsType<BoldInline>(Assert.Single(result));
        var text = Assert.IsType<TextInline>(Assert.Single(bold.Children));
        Assert.Equal("bold", text.Content);
    }

    [Fact]
    public void ParseInlines_Italic()
    {
        var result = _parser.ParseInlines("*italic*");
        var italic = Assert.IsType<ItalicInline>(Assert.Single(result));
        var text = Assert.IsType<TextInline>(Assert.Single(italic.Children));
        Assert.Equal("italic", text.Content);
    }

    [Fact]
    public void ParseInlines_Code()
    {
        var result = _parser.ParseInlines("`code`");
        var code = Assert.IsType<CodeInline>(Assert.Single(result));
        Assert.Equal("code", code.Code);
    }

    [Fact]
    public void ParseInlines_Link()
    {
        var result = _parser.ParseInlines("[text](http://example.com)");
        var link = Assert.IsType<LinkInline>(Assert.Single(result));
        Assert.Equal("http://example.com", link.Url);
        var text = Assert.IsType<TextInline>(Assert.Single(link.Children));
        Assert.Equal("text", text.Content);
    }

    [Fact]
    public void ParseInlines_Image()
    {
        var result = _parser.ParseInlines("![alt](image.png)");
        var img = Assert.IsType<ImageInline>(Assert.Single(result));
        Assert.Equal("image.png", img.Url);
        Assert.Equal("alt", img.Alt);
    }

    [Fact]
    public void ParseInlines_Strikethrough()
    {
        var result = _parser.ParseInlines("~~deleted~~");
        var del = Assert.IsType<StrikethroughInline>(Assert.Single(result));
        var text = Assert.IsType<TextInline>(Assert.Single(del.Children));
        Assert.Equal("deleted", text.Content);
    }

    #endregion

    #region Table (GFM)

    [Fact]
    public void Parse_Table_ReturnsTableBlock()
    {
        var md = "| H1 | H2 |\n| --- | --- |\n| A | B |";
        var result = _parser.Parse(md);
        var table = Assert.IsType<TableBlock>(Assert.Single(result));
        Assert.Equal(2, table.Headers.Count);
        Assert.Equal("H1", table.Headers[0]);
        Assert.Equal("H2", table.Headers[1]);
        Assert.Single(table.Rows);
        Assert.Equal("A", table.Rows[0][0]);
        Assert.Equal("B", table.Rows[0][1]);
    }

    #endregion

    #region HTML Renderer

    [Fact]
    public void HtmlRenderer_Heading_ProducesCorrectHtml()
    {
        var blocks = _parser.Parse("# Hello");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Equal("<h1>Hello</h1>", html);
    }

    [Fact]
    public void HtmlRenderer_Paragraph_ProducesCorrectHtml()
    {
        var blocks = _parser.Parse("Hello world");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Equal("<p>Hello world</p>", html);
    }

    [Fact]
    public void HtmlRenderer_Bold_ProducesStrongTag()
    {
        var blocks = _parser.Parse("**bold text**");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Equal("<p><strong>bold text</strong></p>", html);
    }

    [Fact]
    public void HtmlRenderer_CodeBlock_ProducesPreCodeTags()
    {
        var blocks = _parser.Parse("```\ncode\n```");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Equal("<pre><code>code</code></pre>", html);
    }

    #endregion
}
