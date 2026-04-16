namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Registry for language highlighters. Enables extending the language set
/// without changing dispatcher logic.
/// </summary>
public sealed class SyntaxHighlighterRegistry
{
    private readonly List<ISyntaxHighlighter> _highlighters = [];

    public SyntaxHighlighterRegistry Register(ISyntaxHighlighter highlighter)
    {
        _highlighters.Add(highlighter);
        return this;
    }

    public bool AnySupports(string language) =>
        _highlighters.Exists(h => h.SupportsLanguage(language));

    public ISyntaxHighlighter? Find(string language) =>
        _highlighters.Find(h => h.SupportsLanguage(language));
}
