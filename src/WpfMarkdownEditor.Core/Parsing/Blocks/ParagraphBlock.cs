using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class ParagraphBlock : Block
{
    public List<Inline> Inlines { get; set; } = [];
}
