namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Tracks parser state during line-by-line processing.
/// </summary>
internal sealed class ParserState
{
    public bool InCodeBlock { get; set; }
    public string? CodeFenceChar { get; set; } // "`" or "~"
    public int CodeFenceLength { get; set; }
    public string? CodeLanguage { get; set; }
    public int CodeBlockLineStart { get; set; }
    public bool InBlockquote { get; set; }
    public int BlockquoteDepth { get; set; }
    public bool InList { get; set; }
    public int ListIndent { get; set; }

    public void Reset()
    {
        InCodeBlock = false;
        CodeFenceChar = null;
        CodeFenceLength = 0;
        CodeLanguage = null;
        InBlockquote = false;
        BlockquoteDepth = 0;
        InList = false;
        ListIndent = 0;
    }

    public ParserState Snapshot() => new()
    {
        InCodeBlock = InCodeBlock,
        CodeFenceChar = CodeFenceChar,
        CodeFenceLength = CodeFenceLength,
        CodeLanguage = CodeLanguage,
        CodeBlockLineStart = CodeBlockLineStart,
        InBlockquote = InBlockquote,
        BlockquoteDepth = BlockquoteDepth,
        InList = InList,
        ListIndent = ListIndent,
    };
}
