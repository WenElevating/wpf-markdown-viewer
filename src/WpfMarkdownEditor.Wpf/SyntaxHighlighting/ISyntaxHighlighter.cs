namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Highlights source code by producing typed tokens.
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>
    /// Returns true if this highlighter supports the given language.
    /// </summary>
    bool SupportsLanguage(string language);

    /// <summary>
    /// Tokenize source code into syntax tokens.
    /// </summary>
    List<SyntaxToken> Tokenize(string code);
}
