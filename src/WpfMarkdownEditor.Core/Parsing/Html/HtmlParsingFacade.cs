namespace WpfMarkdownEditor.Core.Parsing.Html;

internal sealed class HtmlParsingFacade
{
    private const int MaxInlineScanLength = 8192;

    private static readonly HashSet<string> BlockStartTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "div", "p", "center", "details", "summary", "table",
        "h1", "h2", "h3", "h4", "h5", "h6"
    };

    private static readonly HashSet<string> InlineStartTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "b", "strong", "i", "em", "code", "a", "img", "picture", "source"
    };

    private static readonly HashSet<string> SupportedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "div", "p", "center", "details", "summary",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "table", "thead", "tbody", "tr", "td", "th",
        "a", "picture", "source", "img", "br", "b", "strong", "i", "em", "code"
    };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "img", "source", "hr", "input"
    };

    public bool TryParseBlockFragment(
        string source,
        int startIndex,
        out HtmlFragment fragment,
        out int consumedLineCount)
    {
        fragment = new HtmlFragment { Kind = HtmlFragmentKind.Block };
        consumedLineCount = 0;

        if (!TryReadTagAt(source, startIndex, out var firstTag) ||
            firstTag.Kind != HtmlTagScanKind.Open && firstTag.Kind != HtmlTagScanKind.SelfClose ||
            !BlockStartTags.Contains(firstTag.Name))
        {
            return false;
        }

        var lineStart = FindLineStart(source, startIndex);
        var lineEnd = FindLineEndIncludingTerminator(source, startIndex);
        var scanPosition = startIndex;
        var stack = new Stack<string>();
        var consumedEnd = lineEnd;

        var complete = false;
        while (scanPosition < source.Length)
        {
            while (scanPosition < lineEnd && TryReadTagAtOrAfter(source, scanPosition, lineEnd, out var tag))
            {
                ApplyTag(tag, stack);
                consumedEnd = lineEnd;
                scanPosition = tag.EndIndexExclusive;

                if (stack.Count == 0)
                {
                    complete = true;
                    break;
                }
            }

            if (complete)
                break;

            if (lineEnd >= source.Length)
                break;

            scanPosition = lineEnd;
            lineEnd = FindLineEndIncludingTerminator(source, scanPosition);
        }

        var html = source[lineStart..consumedEnd];
        fragment = new HtmlSubsetParser().Parse(html, HtmlFragmentKind.Block);
        consumedLineCount = CountLines(source.AsSpan(lineStart, consumedEnd - lineStart));
        return fragment.Children.Count > 0 && consumedLineCount > 0;
    }

    public bool TryParseInlineFragment(
        string source,
        int startIndex,
        out HtmlFragment fragment,
        out int consumedLength)
    {
        fragment = new HtmlFragment { Kind = HtmlFragmentKind.Inline };
        consumedLength = 0;

        if (!TryReadTagAt(source, startIndex, out var firstTag) ||
            !InlineStartTags.Contains(firstTag.Name) ||
            firstTag.Kind is not (HtmlTagScanKind.Open or HtmlTagScanKind.SelfClose))
        {
            return false;
        }

        var end = firstTag.EndIndexExclusive;
        if (firstTag.Kind == HtmlTagScanKind.Open && !VoidTags.Contains(firstTag.Name))
        {
            var stack = new Stack<string>();
            ApplyTag(firstTag, stack);
            var scanPosition = firstTag.EndIndexExclusive;
            var scanLimit = Math.Min(source.Length, startIndex + MaxInlineScanLength);

            while (scanPosition < scanLimit && stack.Count > 0)
            {
                if (!TryReadTagAtOrAfter(source, scanPosition, scanLimit, out var tag))
                    return false;

                if (!InlineStartTags.Contains(tag.Name))
                    return false;

                ApplyTag(tag, stack);
                scanPosition = tag.EndIndexExclusive;
                end = tag.EndIndexExclusive;
            }

            if (stack.Count > 0)
                return false;
        }

        var html = source[startIndex..end];
        fragment = new HtmlSubsetParser().Parse(html, HtmlFragmentKind.Inline);
        consumedLength = end - startIndex;
        return fragment.Children.Count > 0 && consumedLength > 0;
    }

    private static void ApplyTag(HtmlTagScan tag, Stack<string> stack)
    {
        if (!SupportedTags.Contains(tag.Name))
            return;

        switch (tag.Kind)
        {
            case HtmlTagScanKind.Open:
                if (!VoidTags.Contains(tag.Name))
                    stack.Push(tag.Name);
                break;

            case HtmlTagScanKind.SelfClose:
                break;

            case HtmlTagScanKind.Close:
                if (stack.Count > 0 && string.Equals(stack.Peek(), tag.Name, StringComparison.OrdinalIgnoreCase))
                    stack.Pop();
                break;
        }
    }

    private static bool TryReadTagAtOrAfter(string source, int startIndex, int scanLimit, out HtmlTagScan tag)
    {
        var position = startIndex;
        while (position < scanLimit)
        {
            var next = source.IndexOf('<', position);
            if (next < 0 || next >= scanLimit)
                break;

            if (TryReadTagAt(source, next, out tag))
                return true;

            position = next + 1;
        }

        tag = default;
        return false;
    }

    private static bool TryReadTagAt(string source, int startIndex, out HtmlTagScan tag)
    {
        tag = default;
        if (startIndex < 0 || startIndex >= source.Length || source[startIndex] != '<')
            return false;

        var position = startIndex + 1;
        if (position >= source.Length)
            return false;

        if (source.AsSpan(startIndex).StartsWith("<!--", StringComparison.Ordinal))
        {
            var commentEnd = source.IndexOf("-->", startIndex, StringComparison.Ordinal);
            if (commentEnd < 0)
                return false;

            tag = new HtmlTagScan(HtmlTagScanKind.Comment, string.Empty, commentEnd + 3);
            return true;
        }

        if (source[position] == '!')
        {
            var declarationEnd = FindTagEnd(source, startIndex);
            if (declarationEnd < 0)
                return false;

            tag = new HtmlTagScan(HtmlTagScanKind.Comment, string.Empty, declarationEnd + 1);
            return true;
        }

        var isClose = source[position] == '/';
        if (isClose)
            position++;

        var nameStart = position;
        while (position < source.Length && IsNameChar(source[position]))
            position++;

        if (position == nameStart)
            return false;

        var name = source[nameStart..position].ToLowerInvariant();
        var tagEnd = FindTagEnd(source, startIndex);
        if (tagEnd < 0)
            return false;

        if (isClose)
        {
            tag = new HtmlTagScan(HtmlTagScanKind.Close, name, tagEnd + 1);
            return true;
        }

        var selfClose = IsExplicitSelfClose(source, startIndex, tagEnd) || VoidTags.Contains(name);
        tag = new HtmlTagScan(selfClose ? HtmlTagScanKind.SelfClose : HtmlTagScanKind.Open, name, tagEnd + 1);
        return true;
    }

    private static bool IsExplicitSelfClose(string source, int startIndex, int tagEnd)
    {
        var position = tagEnd - 1;
        while (position > startIndex && char.IsWhiteSpace(source[position]))
            position--;
        return position > startIndex && source[position] == '/';
    }

    private static int FindTagEnd(string source, int startIndex)
    {
        var quote = '\0';
        for (var i = startIndex + 1; i < source.Length; i++)
        {
            var c = source[i];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }

            if (c == '>')
                return i;
        }

        return -1;
    }

    private static int FindLineStart(string source, int startIndex)
    {
        var position = startIndex;
        while (position > 0 && source[position - 1] is not ('\n' or '\r'))
            position--;
        return position;
    }

    private static int FindLineEndIncludingTerminator(string source, int startIndex)
    {
        var position = startIndex;
        while (position < source.Length && source[position] is not ('\n' or '\r'))
            position++;

        if (position < source.Length && source[position] == '\r')
        {
            position++;
            if (position < source.Length && source[position] == '\n')
                position++;
            return position;
        }

        if (position < source.Length && source[position] == '\n')
            position++;

        return position;
    }

    private static int CountLines(ReadOnlySpan<char> text)
    {
        if (text.Length == 0)
            return 0;

        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                count++;
        }

        if (text[^1] == '\n')
            count--;

        return count;
    }

    private static bool IsNameChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '_' or ':' or '-';

    private readonly record struct HtmlTagScan(HtmlTagScanKind Kind, string Name, int EndIndexExclusive);

    private enum HtmlTagScanKind
    {
        Comment,
        Open,
        Close,
        SelfClose
    }
}
