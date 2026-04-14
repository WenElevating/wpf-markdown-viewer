using WpfMarkdownEditor.Core.Parsing.Blocks;

namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Parses Markdown text into a Block AST.
/// Implements CommonMark spec with GFM extensions (tables, strikethrough).
/// </summary>
public sealed class MarkdownParser
{
    /// <summary>
    /// Parse Markdown text into a list of blocks.
    /// </summary>
    public List<Block> Parse(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return [];

        var reader = new LineReader(markdown);
        var blockParser = new BlockParser();
        return blockParser.ParseBlocks(reader);
    }

    /// <summary>
    /// Parse only inline elements within a text range.
    /// </summary>
    public List<Inline> ParseInlines(string text)
    {
        var parser = new InlineParser();
        return parser.ParseInlines(text);
    }
}
