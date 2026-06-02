using WpfMarkdownEditor.Core.Parsing.Html;
using Xunit;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public sealed class HtmlSubsetParserTests
{
    [Fact]
    public void Parse_NestedSameNameTags_PreservesTree()
    {
        var fragment = new HtmlSubsetParser().Parse("<div><div>inner</div></div>", HtmlFragmentKind.Block);

        var outer = Assert.IsType<HtmlElementNode>(Assert.Single(fragment.Children));
        var inner = Assert.IsType<HtmlElementNode>(Assert.Single(outer.Children));
        var text = Assert.IsType<HtmlTextNode>(Assert.Single(inner.Children));
        Assert.Equal("div", outer.TagName);
        Assert.Equal("div", inner.TagName);
        Assert.Equal("inner", text.Text);
    }

    [Fact]
    public void Parse_MismatchedCloseTag_DoesNotThrow()
    {
        var fragment = new HtmlSubsetParser().Parse("<div>text</span>", HtmlFragmentKind.Block);

        var div = Assert.IsType<HtmlElementNode>(Assert.Single(fragment.Children));
        Assert.Equal("div", div.TagName);
        Assert.IsType<HtmlTextNode>(Assert.Single(div.Children));
    }

    [Fact]
    public void Parse_ImplicitParagraphClose_ClosesPreviousParagraph()
    {
        var fragment = new HtmlSubsetParser().Parse("<p>one<p>two</p>", HtmlFragmentKind.Block);

        Assert.Equal(2, fragment.Children.Count);
        var first = Assert.IsType<HtmlElementNode>(fragment.Children[0]);
        var second = Assert.IsType<HtmlElementNode>(fragment.Children[1]);
        Assert.Equal("one", Assert.IsType<HtmlTextNode>(Assert.Single(first.Children)).Text);
        Assert.Equal("two", Assert.IsType<HtmlTextNode>(Assert.Single(second.Children)).Text);
    }

    [Fact]
    public void Parse_UnsupportedTag_FlattensTextToCurrentParent()
    {
        var fragment = new HtmlSubsetParser().Parse("<custom>visible</custom>", HtmlFragmentKind.Inline);

        var text = Assert.IsType<HtmlTextNode>(Assert.Single(fragment.Children));
        Assert.Equal("visible", text.Text);
    }
}
