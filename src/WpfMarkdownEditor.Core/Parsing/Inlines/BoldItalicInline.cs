namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class BoldItalicInline : Inline
{
    public List<Inline> Children { get; set; } = [];
}
