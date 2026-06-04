using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownParagraphOperationsTests
{
    [Fact]
    public void SetHeadingLevel_CurrentLine_AddsRequestedHeadingMarker()
    {
        var result = MarkdownParagraphOperations.SetHeadingLevel("one\ntwo", 4, 0, 4);

        Assert.Equal("one\n#### two", result.Text);
        Assert.Equal(9, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void SetHeadingLevel_ExistingHeading_ReplacesHeadingMarker()
    {
        var result = MarkdownParagraphOperations.SetHeadingLevel("## title", 3, 0, 5);

        Assert.Equal("##### title", result.Text);
        Assert.Equal(6, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void SetHeadingLevel_SelectedLines_UpdatesEachSelectedLine()
    {
        var result = MarkdownParagraphOperations.SetHeadingLevel("one\ntwo\nthree", 0, "one\ntwo".Length, 2);

        Assert.Equal("## one\n## two\nthree", result.Text);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal("## one\n## two".Length, result.SelectionLength);
    }

    [Fact]
    public void ClearBlockPrefix_HeadingLine_RemovesHeadingMarker()
    {
        var result = MarkdownParagraphOperations.ClearBlockPrefix("### title", 5, 0);

        Assert.Equal("title", result.Text);
        Assert.Equal(1, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ClearBlockPrefix_BlockquoteLine_RemovesQuoteMarker()
    {
        var result = MarkdownParagraphOperations.ClearBlockPrefix("> quote", 3, 0);

        Assert.Equal("quote", result.Text);
        Assert.Equal(1, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Theory]
    [InlineData("- item")]
    [InlineData("* item")]
    [InlineData("+ item")]
    [InlineData("12. item")]
    public void ClearBlockPrefix_ListLine_RemovesListMarker(string input)
    {
        var result = MarkdownParagraphOperations.ClearBlockPrefix(input, input.Length, 0);

        Assert.Equal("item", result.Text);
        Assert.Equal("item".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleBlockquote_CurrentLine_TogglesPrefix()
    {
        var added = MarkdownParagraphOperations.ToggleBlockquote("one", 1, 0);
        var removed = MarkdownParagraphOperations.ToggleBlockquote(added.Text, 3, 0);

        Assert.Equal("> one", added.Text);
        Assert.Equal("one", removed.Text);
    }

    [Fact]
    public void ToggleBulletList_CurrentLine_TogglesPrefix()
    {
        var added = MarkdownParagraphOperations.ToggleBulletList("one", 1, 0);
        var removed = MarkdownParagraphOperations.ToggleBulletList(added.Text, 3, 0);

        Assert.Equal("- one", added.Text);
        Assert.Equal("one", removed.Text);
    }

    [Fact]
    public void ToggleOrderedList_CurrentLine_TogglesPrefix()
    {
        var added = MarkdownParagraphOperations.ToggleOrderedList("one", 1, 0);
        var removed = MarkdownParagraphOperations.ToggleOrderedList(added.Text, 4, 0);

        Assert.Equal("1. one", added.Text);
        Assert.Equal("one", removed.Text);
    }

    [Fact]
    public void InsertParagraphAbove_CurrentLine_InsertsBlankParagraphBeforeLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphAbove("one\ntwo", 5, 0);

        Assert.Equal("one\n\ntwo", result.Text);
        Assert.Equal("one\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertParagraphBelow_CurrentLine_InsertsBlankParagraphAfterLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphBelow("one\ntwo", 1, 0);

        Assert.Equal("one\n\ntwo", result.Text);
        Assert.Equal("one\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertParagraphAbove_SelectedLines_InsertsBeforeFirstSelectedLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphAbove("one\ntwo\nthree", 4, "two\nthree".Length);

        Assert.Equal("one\n\ntwo\nthree", result.Text);
        Assert.Equal("one\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertParagraphBelow_SelectedLines_InsertsAfterLastSelectedLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphBelow("one\ntwo\nthree", 0, "one\ntwo".Length);

        Assert.Equal("one\ntwo\n\nthree", result.Text);
        Assert.Equal("one\ntwo\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertHorizontalRule_BetweenText_AddsBlankLineBoundaries()
    {
        var result = MarkdownParagraphOperations.InsertHorizontalRule("one\ntwo", 4, 0);

        Assert.Equal("one\n\n---\n\ntwo", result.Text);
        Assert.Equal("one\n\n---\n\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertHorizontalRule_ReplacesSelection()
    {
        var result = MarkdownParagraphOperations.InsertHorizontalRule("one selected two", 4, "selected".Length);
        var newline = Environment.NewLine;

        Assert.Equal("one " + newline + newline + "---" + newline + newline + " two", result.Text);
        Assert.Equal(("one " + newline + newline + "---" + newline + newline).Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }
}
