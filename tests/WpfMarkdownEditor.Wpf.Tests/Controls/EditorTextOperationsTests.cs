using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class EditorTextOperationsTests
{
    [Fact]
    public void MoveLineUp_CurrentLineSwapsWithPreviousLine()
    {
        var result = EditorTextOperations.MoveSelectedLines("one\ntwo\nthree", 4, 0, -1);

        Assert.NotNull(result);
        Assert.Equal("two\none\nthree", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal(3, result.Value.SelectionLength);
    }

    [Fact]
    public void MoveLineUp_SelectedBlockStartsOnFirstLine_ReturnsNull()
    {
        var result = EditorTextOperations.MoveSelectedLines("one\ntwo\nthree", 0, 7, -1);

        Assert.Null(result);
    }

    [Fact]
    public void MoveLineDown_CurrentLineSwapsWithNextLine()
    {
        var result = EditorTextOperations.MoveSelectedLines("one\ntwo\nthree", 4, 0, 1);

        Assert.NotNull(result);
        Assert.Equal("one\nthree\ntwo", result.Value.Text);
        Assert.Equal(10, result.Value.SelectionStart);
        Assert.Equal(3, result.Value.SelectionLength);
    }

    [Fact]
    public void MoveLineDown_SelectedBlockEndsOnLastLine_ReturnsNull()
    {
        var result = EditorTextOperations.MoveSelectedLines("one\ntwo\nthree", 4, 9, 1);

        Assert.Null(result);
    }

    [Fact]
    public void DeleteSelectionOrCurrentLine_NoSelectionDeletesCurrentLine()
    {
        var result = EditorTextOperations.DeleteSelectionOrCurrentLine("one\ntwo\nthree", 4, 0);

        Assert.NotNull(result);
        Assert.Equal("one\nthree", result.Value.Text);
        Assert.Equal(4, result.Value.SelectionStart);
        Assert.Equal(0, result.Value.SelectionLength);
    }

    [Fact]
    public void DeleteSelectionOrCurrentLine_SelectionDeletesSelection()
    {
        var result = EditorTextOperations.DeleteSelectionOrCurrentLine("one two", 3, 1);

        Assert.NotNull(result);
        Assert.Equal("onetwo", result.Value.Text);
        Assert.Equal(3, result.Value.SelectionStart);
        Assert.Equal(0, result.Value.SelectionLength);
    }

    [Fact]
    public void DeleteSelectionOrCurrentLine_EmptyDocument_ReturnsNull()
    {
        var result = EditorTextOperations.DeleteSelectionOrCurrentLine("", 0, 0);

        Assert.Null(result);
    }

    [Fact]
    public void InsertText_ReplacesSelectionAndMovesCaret()
    {
        var result = EditorTextOperations.InsertText("one two", 3, 1, "  \n");

        Assert.Equal("one  \ntwo", result.Text);
        Assert.Equal(6, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertImageBlock_BetweenHtmlBlocks_AddsBlankLineBoundaries()
    {
        var result = EditorTextOperations.InsertImageBlock(
            "<h1>Title</h1>\n<p>Next</p>",
            "<h1>Title</h1>\n".Length,
            0,
            "![clipboard](images/clipboard.png)");

        Assert.Equal("<h1>Title</h1>\n\n![clipboard](images/clipboard.png)\n\n<p>Next</p>", result.Text);
        Assert.Equal("<h1>Title</h1>\n\n![clipboard](images/clipboard.png)\n\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertImageBlock_EmptyDocument_InsertsImageOnly()
    {
        var result = EditorTextOperations.InsertImageBlock(
            string.Empty,
            0,
            0,
            "![clipboard](images/clipboard.png)");

        Assert.Equal("![clipboard](images/clipboard.png)", result.Text);
        Assert.Equal("![clipboard](images/clipboard.png)".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }
}
