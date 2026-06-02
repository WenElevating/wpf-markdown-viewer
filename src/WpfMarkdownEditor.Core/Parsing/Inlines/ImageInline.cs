namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class ImageInline : Inline
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public string? Title { get; set; }
    public double? DisplayWidth { get; set; }
    public double? DisplayHeight { get; set; }
}
