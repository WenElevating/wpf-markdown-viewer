namespace WpfMarkdownEditor.Core.Parsing.Html;

public enum HtmlFragmentKind
{
    Block,
    Inline
}

public sealed class HtmlFragment
{
    public required HtmlFragmentKind Kind { get; init; }
    public List<HtmlNode> Children { get; } = [];
}

public abstract class HtmlNode;

public sealed class HtmlTextNode : HtmlNode
{
    public required string Text { get; init; }
}

public sealed class HtmlElementNode : HtmlNode
{
    public required string TagName { get; init; }
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<HtmlNode> Children { get; } = [];
    public bool IsVoidElement { get; init; }
}
