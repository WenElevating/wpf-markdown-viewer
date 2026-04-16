using WpfMarkdownEditor.Wpf.SyntaxHighlighting;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.SyntaxHighlighting;

public sealed class SyntaxHighlighterTests
{
    [Theory]
    [InlineData("node")]
    [InlineData("nodejs")]
    [InlineData("py3")]
    [InlineData("c#")]
    [InlineData("jsonc")]
    [InlineData("postgres")]
    [InlineData("zsh")]
    public void SupportsLanguage_Aliases_AreRecognized(string language)
    {
        var highlighter = new SyntaxHighlighter();
        Assert.True(highlighter.SupportsLanguage(language));
    }

    [Fact]
    public void Tokenize_UnknownLanguage_FallsBackToPlain()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("select 1", "foobarlang");

        var token = Assert.Single(tokens);
        Assert.Equal(TokenType.Plain, token.Type);
        Assert.Equal("select 1", token.Text);
    }

    [Fact]
    public void Tokenize_NodeAlias_UsesJavaScriptLexer()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("const a = 1", "node");

        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "const");
    }

    [Fact]
    public void Tokenize_Jsonc_ParsesBooleansAsKeywords()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("{\"ok\": true}", "jsonc");

        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "true");
    }

    [Fact]
    public void Tokenize_PostgresAlias_UsesSqlLexer()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("select * from users", "postgres");

        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "select");
        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "from");
    }

    [Fact]
    public void Tokenize_ZshAlias_UsesBashLexer()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("if [ -n \"$HOME\" ]; then echo ok; fi", "zsh");

        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "if");
        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "then");
        Assert.Contains(tokens, t => t.Type == TokenType.Keyword && t.Text == "fi");
    }

    [Fact]
    public void Tokenize_Sql_EscapedString_IsSingleStringToken()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("select 'it''s ok' as txt", "sql");

        Assert.Contains(tokens, t => t.Type == TokenType.String && t.Text == "'it''s ok'");
    }

    [Fact]
    public void Tokenize_Jsonc_CommentsAndExponent_AreRecognized()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("// c\n{\"n\": -1.2e3}", "jsonc");

        Assert.Contains(tokens, t => t.Type == TokenType.Comment && t.Text == "// c");
        Assert.Contains(tokens, t => t.Type == TokenType.Number && t.Text == "-1.2e3");
    }

    [Fact]
    public void Tokenize_Bash_CommentAndVariable_AreRecognized()
    {
        var highlighter = new SyntaxHighlighter();
        var tokens = highlighter.Tokenize("echo $HOME # note", "bash");

        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Text == "$HOME");
        Assert.Contains(tokens, t => t.Type == TokenType.Comment && t.Text == "# note");
    }

    [Fact]
    public void Tokenize_SameInput_UsesCachedTokens()
    {
        var highlighter = new SyntaxHighlighter();
        var first = highlighter.Tokenize("select 1", "sql");
        var second = highlighter.Tokenize("select 1", "sql");

        Assert.Same(first, second);
    }
}
