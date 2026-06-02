namespace WpfMarkdownEditor.Core.Parsing.Html;

internal sealed class HtmlSubsetParser
{
    private static readonly HashSet<string> SupportedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "div", "p", "center", "details", "summary",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "thead", "tbody", "tr", "td", "th",
        "a", "img", "br", "b", "strong", "i", "em", "code"
    };

    public HtmlFragment Parse(string html, HtmlFragmentKind kind)
    {
        var fragment = new HtmlFragment { Kind = kind };
        var nodeStack = new Stack<List<HtmlNode>>();
        var elementStack = new Stack<HtmlElementNode>();
        nodeStack.Push(fragment.Children);

        foreach (var token in new HtmlTokenizer(html).Tokenize())
        {
            switch (token.Kind)
            {
                case HtmlTokenKind.Text:
                    if (!string.IsNullOrEmpty(token.Text))
                        nodeStack.Peek().Add(new HtmlTextNode { Text = token.Text });
                    break;

                case HtmlTokenKind.OpenTag:
                    if (!SupportedTags.Contains(token.Name))
                        break;

                    if (token.Name == "p" &&
                        elementStack.Count > 0 &&
                        string.Equals(elementStack.Peek().TagName, "p", StringComparison.OrdinalIgnoreCase))
                    {
                        nodeStack.Pop();
                        elementStack.Pop();
                    }

                    var element = CreateElement(token, isVoid: false);
                    nodeStack.Peek().Add(element);
                    elementStack.Push(element);
                    nodeStack.Push(element.Children);
                    break;

                case HtmlTokenKind.SelfClose:
                    if (SupportedTags.Contains(token.Name))
                        nodeStack.Peek().Add(CreateElement(token, isVoid: true));
                    break;

                case HtmlTokenKind.CloseTag:
                    if (elementStack.Count > 0 &&
                        string.Equals(elementStack.Peek().TagName, token.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        nodeStack.Pop();
                        elementStack.Pop();
                    }
                    break;
            }
        }

        return fragment;
    }

    private static HtmlElementNode CreateElement(HtmlToken token, bool isVoid) =>
        new()
        {
            TagName = token.Name.ToLowerInvariant(),
            Attributes = token.Attributes,
            IsVoidElement = isVoid
        };
}
