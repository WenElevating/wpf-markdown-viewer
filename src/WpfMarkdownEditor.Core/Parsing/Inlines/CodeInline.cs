namespace WpfMarkdownEditor.Core.Parsing.Inlines;

public sealed class CodeInline : Inline
{
    public string Code { get; set; } = string.Empty;
}
