namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class StrikethroughInline : Inline
{
    public List<Inline> Children { get; set; } = [];
}
