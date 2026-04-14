namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Base class for all inline Markdown elements within blocks.
/// </summary>
public abstract class Inline
{
    public int SourceOffset { get; set; }
    public int SourceLength { get; set; }
}
