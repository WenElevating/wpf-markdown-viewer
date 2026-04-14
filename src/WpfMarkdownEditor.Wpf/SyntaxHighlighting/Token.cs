namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Syntax token types for code highlighting.
/// </summary>
public enum TokenType
{
    Keyword,
    Comment,
    String,
    Number,
    Identifier,
    Operator,
    Punctuation,
    Whitespace,
    Type,       // Built-in types (int, string, var, etc.)
    Plain       // Default / unclassified
}

/// <summary>
/// A single syntax token with type and text span.
/// </summary>
public sealed record SyntaxToken(TokenType Type, string Text);
