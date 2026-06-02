using WpfMarkdownEditor.Core.Parsing.Html;

namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class HtmlInline : Inline
{
    public required HtmlFragment Fragment { get; init; }
}
