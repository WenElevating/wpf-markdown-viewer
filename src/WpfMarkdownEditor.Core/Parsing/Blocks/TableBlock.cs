namespace WpfMarkdownEditor.Core.Parsing.Blocks;

public sealed class TableBlock : Block
{
    public List<string> Headers { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public List<TableAlignment> Alignments { get; set; } = [];

    public enum TableAlignment { Left, Center, Right }
}
