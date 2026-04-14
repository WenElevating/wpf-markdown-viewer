using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class HeadingBlock : Block
{
    public int Level { get; set; } // 1-6
    public List<Inline> Inlines { get; set; } = [];
}
