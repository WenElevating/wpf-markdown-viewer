using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class BlockquoteBlock : Block
{
    public List<Block> Children { get; set; } = [];
}
