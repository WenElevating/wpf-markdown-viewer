namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Base class for all block-level Markdown elements.
/// </summary>
public abstract class Block
{
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public int ColumnStart { get; set; }
}
