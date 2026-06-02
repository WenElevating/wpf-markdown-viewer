using WpfMarkdownEditor.Core.Parsing.Html;
using Xunit;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public sealed class HtmlTokenizerTests
{
    [Fact]
    public void Tokenize_TextAndTags_ReturnsExpectedTokens()
    {
        var tokens = new HtmlTokenizer("""hello <strong>world</strong><br />""").Tokenize().ToList();

        Assert.Equal(5, tokens.Count);
        Assert.Equal(HtmlTokenKind.Text, tokens[0].Kind);
        Assert.Equal("hello ", tokens[0].Text);
        Assert.Equal(HtmlTokenKind.OpenTag, tokens[1].Kind);
        Assert.Equal("strong", tokens[1].Name);
        Assert.Equal(HtmlTokenKind.Text, tokens[2].Kind);
        Assert.Equal("world", tokens[2].Text);
        Assert.Equal(HtmlTokenKind.CloseTag, tokens[3].Kind);
        Assert.Equal("strong", tokens[3].Name);
        Assert.Equal(HtmlTokenKind.SelfClose, tokens[4].Kind);
        Assert.Equal("br", tokens[4].Name);
    }

    [Theory]
    [InlineData("<br>")]
    [InlineData("<br/>")]
    [InlineData("<br />")]
    [InlineData("<IMG SRC='logo.png' alt=Logo>")]
    public void Tokenize_VoidTags_ReturnsSelfClose(string input)
    {
        var token = Assert.Single(new HtmlTokenizer(input).Tokenize());

        Assert.Equal(HtmlTokenKind.SelfClose, token.Kind);
        Assert.True(token.Name is "br" or "img");
    }

    [Fact]
    public void Tokenize_Attributes_DecodesQuotedAndUnquotedValues()
    {
        var token = Assert.Single(new HtmlTokenizer("""<img SRC="a&amp;b.png" alt='A &lt; B' width=200>""").Tokenize());

        Assert.Equal("img", token.Name);
        Assert.Equal("a&b.png", token.Attributes["src"]);
        Assert.Equal("A < B", token.Attributes["alt"]);
        Assert.Equal("200", token.Attributes["width"]);
    }

    [Fact]
    public void Tokenize_CommentsAndDeclarations_AreSkipped()
    {
        var tokens = new HtmlTokenizer("""before<!-- hidden --><!doctype html><br>after""").Tokenize().ToList();

        Assert.Equal(3, tokens.Count);
        Assert.Equal("before", tokens[0].Text);
        Assert.Equal(HtmlTokenKind.SelfClose, tokens[1].Kind);
        Assert.Equal("after", tokens[2].Text);
    }

    [Fact]
    public void Tokenize_MalformedTag_DegradesToText()
    {
        var tokens = new HtmlTokenizer("before < broken after").Tokenize().ToList();

        Assert.All(tokens, token => Assert.Equal(HtmlTokenKind.Text, token.Kind));
        Assert.Equal("before < broken after", string.Concat(tokens.Select(token => token.Text)));
    }
}
