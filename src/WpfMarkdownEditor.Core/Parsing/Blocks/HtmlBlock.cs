using WpfMarkdownEditor.Core.Parsing.Html;

namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class HtmlBlock : Block
{
    public required HtmlFragment Fragment { get; init; }
}
