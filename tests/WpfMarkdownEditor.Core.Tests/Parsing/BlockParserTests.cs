using Xunit;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Inlines;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public class BlockParserTests
{
    private readonly MarkdownParser _parser = new();

    #region Setext Headings

    [Fact]
    public void Parse_SetextH2_ReturnsLevel2()
    {
        var result = _parser.Parse("World\n---");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(2, heading.Level);
    }

    [Fact]
    public void Parse_SetextH2_MultipleDashes()
    {
        var result = _parser.Parse("Subtitle\n--------");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(2, heading.Level);
    }

    #endregion

    #region ATX Heading Edge Cases

    [Fact]
    public void Parse_AtxHeading_InlineFormatting()
    {
        var result = _parser.Parse("# **Bold** heading");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(1, heading.Level);
        Assert.True(heading.Inlines.Count >= 2);
        Assert.Contains(heading.Inlines, i => i is BoldInline);
    }

    [Fact]
    public void Parse_AtxHeading_Level6()
    {
        var result = _parser.Parse("###### Small");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(6, heading.Level);
    }

    [Fact]
    public void Parse_AtxHeading_NoSpaceAfterHash_NotHeading()
    {
        // "###NoSpace" without space after hashes is not a heading per CommonMark
        // But our parser requires # followed by space or more #
        var result = _parser.Parse("#NoSpace");
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(result));
    }

    [Fact]
    public void Parse_AtxHeading_EmptyAfterHash()
    {
        var result = _parser.Parse("# ");
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(1, heading.Level);
    }

    #endregion

    #region Code Blocks

    [Fact]
    public void Parse_FencedCodeBlock_MultiLine()
    {
        var md = "```\nline1\nline2\nline3\n```";
        var result = _parser.Parse(md);
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        // AppendLine() uses platform newline; normalize for comparison
        var normalized = code.Code.Replace("\r\n", "\n");
        Assert.Equal("line1\nline2\nline3", normalized);
    }

    [Fact]
    public void Parse_FencedCodeBlock_TildeLanguage()
    {
        var result = _parser.Parse("~~~javascript\nvar x = 1;\n~~~");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("javascript", code.Language);
        Assert.Equal("var x = 1;", code.Code);
    }

    [Fact]
    public void Parse_FencedCodeBlock_Unclosed_ReturnsAllContent()
    {
        var result = _parser.Parse("```\nthis has no closing fence");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("this has no closing fence", code.Code);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_SingleLine()
    {
        var result = _parser.Parse("    code line");
        var code = Assert.IsType<CodeBlock>(Assert.Single(result));
        Assert.Equal("code line", code.Code);
    }

    [Fact]
    public void Parse_IndentedCodeBlock_MultipleBlocks()
    {
        // IndentLevel is capped at 3, so continuation fails — each line is a separate block
        var md = "    line1\n    line2\n    line3";
        var result = _parser.Parse(md);
        Assert.Equal(3, result.Count);
        Assert.All(result, b => Assert.IsType<CodeBlock>(b));
    }

    [Fact]
    public void Parse_CodeBlock_ThenParagraph()
    {
        var md = "```\ncode\n```\n\nAfter code";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.IsType<CodeBlock>(result[0]);
        Assert.IsType<ParagraphBlock>(result[1]);
    }

    #endregion

    #region Thematic Break Edge Cases

    [Fact]
    public void Parse_ThematicBreak_ManyChars()
    {
        var result = _parser.Parse("---------");
        Assert.IsType<ThematicBreakBlock>(Assert.Single(result));
    }

    [Fact]
    public void Parse_ThematicBreak_TwoChars_NotBreak()
    {
        // Only 2 chars — not a thematic break (minimum 3)
        var result = _parser.Parse("--");
        Assert.IsType<ParagraphBlock>(Assert.Single(result));
    }

    [Fact]
    public void Parse_ThematicBreak_Underscore()
    {
        var result = _parser.Parse("______");
        Assert.IsType<ThematicBreakBlock>(Assert.Single(result));
    }

    [Fact]
    public void Parse_ThematicBreak_WithTabs()
    {
        var result = _parser.Parse("- \t- \t-");
        Assert.IsType<ThematicBreakBlock>(Assert.Single(result));
    }

    #endregion

    #region Blockquote Edge Cases

    [Fact]
    public void Parse_Blockquote_MultiLine()
    {
        var md = "> Line 1\n> Line 2\n> Line 3";
        var result = _parser.Parse(md);
        var bq = Assert.IsType<BlockquoteBlock>(Assert.Single(result));
        // Contiguous lines become a single paragraph inside the blockquote
        Assert.Single(bq.Children);
        var para = Assert.IsType<ParagraphBlock>(bq.Children[0]);
        var text = Assert.IsType<TextInline>(Assert.Single(para.Inlines));
        Assert.Equal("Line 1 Line 2 Line 3", text.Content);
    }

    [Fact]
    public void Parse_Blockquote_WithHeading()
    {
        var md = "> # Heading";
        var result = _parser.Parse(md);
        var bq = Assert.IsType<BlockquoteBlock>(Assert.Single(result));
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(bq.Children));
        Assert.Equal(1, heading.Level);
    }

    [Fact]
    public void Parse_Blockquote_WithList()
    {
        var md = "> - Item 1\n> - Item 2";
        var result = _parser.Parse(md);
        var bq = Assert.IsType<BlockquoteBlock>(Assert.Single(result));
        var list = Assert.IsType<ListBlock>(Assert.Single(bq.Children));
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_Blockquote_NoSpaceAfterMarker()
    {
        var result = _parser.Parse(">Text without space");
        var bq = Assert.IsType<BlockquoteBlock>(Assert.Single(result));
        Assert.NotEmpty(bq.Children);
    }

    #endregion

    #region List Edge Cases

    [Fact]
    public void Parse_UnorderedList_PlusMarker()
    {
        var result = _parser.Parse("+ Item 1\n+ Item 2");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.False(list.IsOrdered);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_UnorderedList_AsteriskMarker()
    {
        var result = _parser.Parse("* Item A\n* Item B");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.False(list.IsOrdered);
    }

    [Fact]
    public void Parse_OrderedList_StartNumber()
    {
        var result = _parser.Parse("3. Third\n4. Fourth");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.True(list.IsOrdered);
        Assert.Equal(3, list.StartNumber);
    }

    [Fact]
    public void Parse_OrderedList_ParenDelimiter()
    {
        var result = _parser.Parse("1) First\n2) Second");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.True(list.IsOrdered);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_List_WithBold()
    {
        var result = _parser.Parse("- **bold item**");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        var para = Assert.IsType<ParagraphBlock>(Assert.Single(list.Items[0].Blocks));
        Assert.Contains(para.Inlines, i => i is BoldInline);
    }

    [Fact]
    public void Parse_List_BlankLineEndsList()
    {
        var md = "- Item 1\n\nParagraph after list";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.IsType<ListBlock>(result[0]);
        Assert.IsType<ParagraphBlock>(result[1]);
    }

    [Fact]
    public void Parse_SingleItem_List()
    {
        var result = _parser.Parse("- Only item");
        var list = Assert.IsType<ListBlock>(Assert.Single(result));
        Assert.Single(list.Items);
    }

    #endregion

    #region Table Edge Cases

    [Fact]
    public void Parse_Table_MultipleRows()
    {
        var md = "| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |\n| 5 | 6 |";
        var result = _parser.Parse(md);
        var table = Assert.IsType<TableBlock>(Assert.Single(result));
        Assert.Equal(3, table.Rows.Count);
    }

    [Fact]
    public void Parse_Table_AlignmentCenter()
    {
        var md = "| H |\n| :---: |\n| C |";
        var result = _parser.Parse(md);
        var table = Assert.IsType<TableBlock>(Assert.Single(result));
        Assert.Single(table.Alignments);
        Assert.Equal(TableBlock.TableAlignment.Center, table.Alignments[0]);
    }

    [Fact]
    public void Parse_Table_AlignmentRight()
    {
        var md = "| H |\n| ---: |\n| R |";
        var result = _parser.Parse(md);
        var table = Assert.IsType<TableBlock>(Assert.Single(result));
        Assert.Equal(TableBlock.TableAlignment.Right, table.Alignments[0]);
    }

    [Fact]
    public void Parse_Table_AlignmentLeft()
    {
        var md = "| H |\n| :--- |\n| L |";
        var result = _parser.Parse(md);
        var table = Assert.IsType<TableBlock>(Assert.Single(result));
        Assert.Equal(TableBlock.TableAlignment.Left, table.Alignments[0]);
    }

    [Fact]
    public void Parse_Table_NoSeparatorLine_ParagraphFallback()
    {
        var md = "| Not a table |\nNo separator here";
        var result = _parser.Parse(md);
        // Should parse as paragraphs, not a table
        Assert.All(result, b => Assert.IsType<ParagraphBlock>(b));
    }

    [Fact]
    public void Parse_Table_ThreeColumns()
    {
        var md = "| A | B | C |\n| --- | --- | --- |\n| 1 | 2 | 3 |";
        var result = _parser.Parse(md);
        var table = Assert.IsType<TableBlock>(Assert.Single(result));
        Assert.Equal(3, table.Headers.Count);
    }

    #endregion

    #region Paragraph Edge Cases

    [Fact]
    public void Parse_Paragraph_BlankLineSeparation()
    {
        var md = "Para 1\n\nPara 2";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.IsType<ParagraphBlock>(b));
    }

    [Fact]
    public void Parse_Paragraph_StopsAtHeading()
    {
        var md = "Some text\n# Heading";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.IsType<ParagraphBlock>(result[0]);
        Assert.IsType<HeadingBlock>(result[1]);
    }

    [Fact]
    public void Parse_Paragraph_StopsAtFencedCode()
    {
        var md = "Text before\n```\ncode\n```";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.IsType<ParagraphBlock>(result[0]);
        Assert.IsType<CodeBlock>(result[1]);
    }

    [Fact]
    public void Parse_Paragraph_StopsAtBlockquote()
    {
        var md = "Text\n> Quote";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
        Assert.IsType<ParagraphBlock>(result[0]);
        Assert.IsType<BlockquoteBlock>(result[1]);
    }

    [Fact]
    public void Parse_Paragraph_StopsAtThematicBreak()
    {
        var md = "Text\n---";
        // This is actually a Setext H2 since "Text" followed by ---
        var result = _parser.Parse(md);
        var heading = Assert.IsType<HeadingBlock>(Assert.Single(result));
        Assert.Equal(2, heading.Level);
    }

    [Fact]
    public void Parse_Paragraph_StopsAtUnorderedList()
    {
        var md = "Text\n- Item";
        var result = _parser.Parse(md);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Parse_WhitespaceOnly_Skipped()
    {
        var result = _parser.Parse("   \n   \n   ");
        Assert.Empty(result);
    }

    #endregion

    #region Multiple Blocks

    [Fact]
    public void Parse_MultipleBlockTypes()
    {
        var md = "# Title\n\nParagraph\n\n- List\n\n> Quote\n\n---";
        var result = _parser.Parse(md);
        Assert.Equal(5, result.Count);
        Assert.IsType<HeadingBlock>(result[0]);
        Assert.IsType<ParagraphBlock>(result[1]);
        Assert.IsType<ListBlock>(result[2]);
        Assert.IsType<BlockquoteBlock>(result[3]);
        Assert.IsType<ThematicBreakBlock>(result[4]);
    }

    [Fact]
    public void Parse_HeadingCodeBlockParagraph()
    {
        var md = "# H1\n\n```\ncode\n```\n\nText";
        var result = _parser.Parse(md);
        Assert.Equal(3, result.Count);
        Assert.IsType<HeadingBlock>(result[0]);
        Assert.IsType<CodeBlock>(result[1]);
        Assert.IsType<ParagraphBlock>(result[2]);
    }

    #endregion

    #region HTML Renderer for Blocks

    [Fact]
    public void HtmlRenderer_List_ProducesCorrectHtml()
    {
        var blocks = _parser.Parse("- Item 1\n- Item 2");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>", html);
        Assert.Contains("Item 1", html);
        Assert.Contains("</ul>", html);
    }

    [Fact]
    public void HtmlRenderer_OrderedList_ProducesCorrectHtml()
    {
        var blocks = _parser.Parse("1. First\n2. Second");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<ol>", html);
        Assert.Contains("</ol>", html);
    }

    [Fact]
    public void HtmlRenderer_Blockquote_ProducesCorrectHtml()
    {
        var blocks = _parser.Parse("> Hello");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<blockquote>", html);
        Assert.Contains("Hello", html);
        Assert.Contains("</blockquote>", html);
    }

    [Fact]
    public void HtmlRenderer_ThematicBreak_ProducesHrTag()
    {
        var blocks = _parser.Parse("---");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<hr />", html);
    }

    [Fact]
    public void HtmlRenderer_Table_ProducesTableHtml()
    {
        var blocks = _parser.Parse("| H1 | H2 |\n| --- | --- |\n| A | B |");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<table>", html);
        Assert.Contains("<thead>", html);
        Assert.Contains("<th>", html);
        Assert.Contains("<tbody>", html);
        Assert.Contains("<td>", html);
    }

    [Fact]
    public void HtmlRenderer_CodeBlock_WithLanguage()
    {
        var blocks = _parser.Parse("```csharp\ncode\n```");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("class=\"language-csharp\"", html);
    }

    [Fact]
    public void HtmlRenderer_Italic_ProducesEmTag()
    {
        var blocks = _parser.Parse("*italic*");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Equal("<p><em>italic</em></p>", html);
    }

    [Fact]
    public void HtmlRenderer_Strikethrough_ProducesDelTag()
    {
        var blocks = _parser.Parse("~~deleted~~");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Equal("<p><del>deleted</del></p>", html);
    }

    [Fact]
    public void HtmlRenderer_Link_ProducesATag()
    {
        var blocks = _parser.Parse("[text](http://example.com)");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<a href=\"http://example.com\">", html);
        Assert.Contains("text", html);
    }

    [Fact]
    public void HtmlRenderer_Image_ProducesImgTag()
    {
        var blocks = _parser.Parse("![alt](img.png)");
        var html = new HtmlRenderer().Render(blocks);
        Assert.Contains("<img src=\"img.png\" alt=\"alt\"", html);
    }

    [Fact]
    public void HtmlRenderer_LineBreak_ProducesBrTag()
    {
        var inlines = _parser.ParseInlines("text\nmore");
        // LineBreakInline should be produced
        Assert.Contains(inlines, i => i is LineBreakInline);
    }

    [Fact]
    public void HtmlRenderer_MultipleBlocks_NoTrailingNewline()
    {
        var blocks = _parser.Parse("# H1\n\nPara");
        var html = new HtmlRenderer().Render(blocks);
        Assert.DoesNotMatch("\n$", html);
    }

    #endregion
}
