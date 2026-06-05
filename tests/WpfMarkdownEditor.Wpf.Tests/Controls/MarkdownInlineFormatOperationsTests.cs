using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownInlineFormatOperationsTests
{
    [Fact]
    public void ClearInlineStyle_NoSelection_ReturnsNull()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("**bold**", 0, 0);

        Assert.Null(result);
    }

    [Fact]
    public void ClearInlineStyle_BoldSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("**bold**", 0, "**bold**".Length);

        Assert.NotNull(result);
        Assert.Equal("bold", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("bold".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_ItalicSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("*italic*", 0, "*italic*".Length);

        Assert.NotNull(result);
        Assert.Equal("italic", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("italic".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_StrikethroughSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("~~gone~~", 0, "~~gone~~".Length);

        Assert.NotNull(result);
        Assert.Equal("gone", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("gone".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_CodeSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("`code`", 0, "`code`".Length);

        Assert.NotNull(result);
        Assert.Equal("code", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("code".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_UnderlineSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("<u>under</u>", 0, "<u>under</u>".Length);

        Assert.NotNull(result);
        Assert.Equal("under", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("under".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_CommentSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("<!-- note -->", 0, "<!-- note -->".Length);

        Assert.NotNull(result);
        Assert.Equal("note", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("note".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_LinkSelection_KeepsLinkText()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(
            "[label](https://example.com)",
            0,
            "[label](https://example.com)".Length);

        Assert.NotNull(result);
        Assert.Equal("label", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("label".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_MixedSelection_RemovesSupportedWrappers()
    {
        var input = "**bold** and *italic* and `code`";
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(input, 0, input.Length);

        Assert.NotNull(result);
        Assert.Equal("bold and italic and code", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("bold and italic and code".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_SelectionWithNoWrapper_ReturnsUnchangedText()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("plain text", 0, "plain text".Length);

        Assert.NotNull(result);
        Assert.Equal("plain text", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("plain text".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_NestedWrapper_ReturnsUnchangedText()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("**_text_**", 0, "**_text_**".Length);

        Assert.NotNull(result);
        Assert.Equal("**_text_**", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("**_text_**".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_PartialWrapper_ReturnsUnchangedText()
    {
        var input = "**text**";
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(input, 2, "text**".Length);

        Assert.NotNull(result);
        Assert.Equal(input, result.Value.Text);
        Assert.Equal(2, result.Value.SelectionStart);
        Assert.Equal("text**".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_SelectionInsideDocument_ReplacesOnlySelection()
    {
        var input = "before **bold** after";
        var start = "before ".Length;
        var length = "**bold**".Length;
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(input, start, length);

        Assert.NotNull(result);
        Assert.Equal("before bold after", result.Value.Text);
        Assert.Equal(start, result.Value.SelectionStart);
        Assert.Equal("bold".Length, result.Value.SelectionLength);
    }
}
