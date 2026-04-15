namespace WpfMarkdownEditor.Wpf.Models;

public sealed class OutlineItem
{
    public int Level { get; init; }
    public string Text { get; init; } = string.Empty;
    public int LineNumber { get; init; }
}
