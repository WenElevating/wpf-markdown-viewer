namespace WpfMarkdownEditor.Wpf.Events;

public sealed class OutlineChangedEventArgs : EventArgs
{
    public List<Models.OutlineItem> Outline { get; init; } = [];
}
