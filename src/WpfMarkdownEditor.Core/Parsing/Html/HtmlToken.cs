namespace WpfMarkdownEditor.Core.Parsing.Html;

internal enum HtmlTokenKind
{
    Text,
    OpenTag,
    CloseTag,
    SelfClose
}

internal readonly record struct HtmlToken(
    HtmlTokenKind Kind,
    string Name,
    IReadOnlyDictionary<string, string> Attributes,
    string? Text)
{
    public static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
