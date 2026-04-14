namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class BoldInline : Inline
{
    public List<Inline> Children { get; set; } = [];
}
