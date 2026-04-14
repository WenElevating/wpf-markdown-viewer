using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class ImageBlock : Block
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public string? Title { get; set; }
}
