using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class CodeBlock : Block
{
    public string? Language { get; set; }
    public string Code { get; set; } = string.Empty;
}
