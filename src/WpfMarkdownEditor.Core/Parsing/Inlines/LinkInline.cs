namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class LinkInline : Inline
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public List<Inline> Children { get; set; } = [];
}
