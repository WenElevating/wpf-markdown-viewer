using WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Dispatches code to the appropriate language lexer.
/// Falls back to plain text when no lexer matches.
/// </summary>
public sealed class SyntaxHighlighter : ISyntaxHighlighter
{
    private readonly List<ISyntaxHighlighter> _lexers;

    public SyntaxHighlighter()
    {
        _lexers =
        [
            new CSharpLexer(),
            new JavaScriptLexer(),
            new PythonLexer()
        ];
    }

    public bool SupportsLanguage(string language)
    {
        return _lexers.Exists(l => l.SupportsLanguage(language));
    }

    public List<SyntaxToken> Tokenize(string code)
    {
        // Plain text — single token
        return [new SyntaxToken(TokenType.Plain, code)];
    }

    /// <summary>
    /// Tokenize code for a specific language. Returns plain tokens if language is unsupported.
    /// </summary>
    public List<SyntaxToken> Tokenize(string code, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return [new SyntaxToken(TokenType.Plain, code)];

        var lexer = _lexers.Find(l => l.SupportsLanguage(language));
        if (lexer is null)
            return [new SyntaxToken(TokenType.Plain, code)];

        return lexer.Tokenize(code);
    }
}
