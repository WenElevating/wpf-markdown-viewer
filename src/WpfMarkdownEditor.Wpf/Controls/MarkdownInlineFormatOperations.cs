using System.Text.RegularExpressions;

namespace WpfMarkdownEditor.Wpf.Controls;

internal static class MarkdownInlineFormatOperations
{
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[(?<text>[^\[\]]+)\]\((?<url>[^()]*)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextEditOperation? ClearInlineStyle(string text, int selectionStart, int selectionLength)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);
        if (length == 0)
            return null;

        var selection = text.Substring(start, length);
        var cleaned = ClearSelection(selection);
        var updated = text.Remove(start, length).Insert(start, cleaned);
        return new TextEditOperation(updated, start, cleaned.Length);
    }

    private static string ClearSelection(string selection)
    {
        if (ContainsNestedWrapper(selection) || HasPartialWrapper(selection))
            return selection;

        var cleaned = selection;
        cleaned = ReplaceWrappedSegments(cleaned, "**", "**");
        cleaned = ReplaceWrappedSegments(cleaned, "~~", "~~");
        cleaned = ReplaceWrappedSegments(cleaned, "`", "`");
        cleaned = ReplaceWrappedSegments(cleaned, "<u>", "</u>");
        cleaned = ReplaceWrappedSegments(cleaned, "<!-- ", " -->", static inner => inner.Trim());
        cleaned = MarkdownLinkPattern.Replace(cleaned, match => match.Groups["text"].Value);
        cleaned = ReplaceWrappedSegments(cleaned, "*", "*");
        return cleaned;
    }

    private static string ReplaceWrappedSegments(
        string text,
        string opening,
        string closing,
        Func<string, string>? transformInner = null)
    {
        var result = text;
        var searchStart = 0;

        while (searchStart < result.Length)
        {
            var openIndex = result.IndexOf(opening, searchStart, StringComparison.Ordinal);
            if (openIndex < 0)
                break;

            var contentStart = openIndex + opening.Length;
            var closeIndex = result.IndexOf(closing, contentStart, StringComparison.Ordinal);
            if (closeIndex < 0)
                break;

            var inner = result[contentStart..closeIndex];
            if (inner.Length == 0)
            {
                searchStart = closeIndex + closing.Length;
                continue;
            }

            var replacement = transformInner is null ? inner : transformInner(inner);
            result = result[..openIndex] + replacement + result[(closeIndex + closing.Length)..];
            searchStart = openIndex + replacement.Length;
        }

        return result;
    }

    private static bool ContainsNestedWrapper(string selection)
    {
        return IsSingleWrapped(selection, "**", "**", out var boldInner) && ContainsAnyWrapper(boldInner)
            || IsSingleWrapped(selection, "*", "*", out var italicInner) && ContainsAnyWrapper(italicInner)
            || IsSingleWrapped(selection, "~~", "~~", out var strikeInner) && ContainsAnyWrapper(strikeInner)
            || IsSingleWrapped(selection, "`", "`", out var codeInner) && ContainsAnyWrapper(codeInner)
            || IsSingleWrapped(selection, "<u>", "</u>", out var underlineInner) && ContainsAnyWrapper(underlineInner)
            || IsSingleWrapped(selection, "<!-- ", " -->", out var commentInner) && ContainsAnyWrapper(commentInner);
    }

    private static bool IsSingleWrapped(string selection, string opening, string closing, out string inner)
    {
        inner = string.Empty;
        if (opening == "*" && (selection.StartsWith("**", StringComparison.Ordinal) ||
            selection.EndsWith("**", StringComparison.Ordinal)))
        {
            return false;
        }

        if (!selection.StartsWith(opening, StringComparison.Ordinal) ||
            !selection.EndsWith(closing, StringComparison.Ordinal) ||
            selection.Length <= opening.Length + closing.Length)
        {
            return false;
        }

        inner = selection[opening.Length..(selection.Length - closing.Length)];
        return true;
    }

    private static bool ContainsAnyWrapper(string text)
    {
        return text.Contains("**", StringComparison.Ordinal)
            || text.Contains("~~", StringComparison.Ordinal)
            || text.Contains('`')
            || text.Contains("<u>", StringComparison.Ordinal)
            || text.Contains("</u>", StringComparison.Ordinal)
            || text.Contains("<!--", StringComparison.Ordinal)
            || text.Contains("-->", StringComparison.Ordinal)
            || MarkdownLinkPattern.IsMatch(text)
            || HasStandaloneMarker(text, '*')
            || HasStandaloneMarker(text, '_');
    }

    private static bool HasPartialWrapper(string selection)
    {
        return HasUnmatchedToken(selection, "**")
            || HasUnmatchedToken(selection, "~~")
            || HasUnmatchedToken(selection, "`")
            || HasUnmatchedPair(selection, "<u>", "</u>")
            || HasUnmatchedPair(selection, "<!-- ", " -->")
            || HasPartialLink(selection)
            || HasUnmatchedStandaloneMarker(selection, '*');
    }

    private static bool HasUnmatchedToken(string selection, string token) =>
        CountOccurrences(selection, token) % 2 != 0;

    private static bool HasUnmatchedPair(string selection, string opening, string closing) =>
        CountOccurrences(selection, opening) != CountOccurrences(selection, closing);

    private static bool HasPartialLink(string selection)
    {
        return CountOccurrences(selection, "[") != CountOccurrences(selection, "]")
            || CountOccurrences(selection, "(") != CountOccurrences(selection, ")");
    }

    private static bool HasUnmatchedStandaloneMarker(string selection, char marker)
    {
        var count = 0;
        for (var i = 0; i < selection.Length; i++)
        {
            if (selection[i] != marker)
                continue;

            var previousIsMarker = i > 0 && selection[i - 1] == marker;
            var nextIsMarker = i + 1 < selection.Length && selection[i + 1] == marker;
            if (!previousIsMarker && !nextIsMarker)
                count++;
        }

        return count % 2 != 0;
    }

    private static bool HasStandaloneMarker(string text, char marker)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != marker)
                continue;

            var previousIsMarker = i > 0 && text[i - 1] == marker;
            var nextIsMarker = i + 1 < text.Length && text[i + 1] == marker;
            if (!previousIsMarker && !nextIsMarker)
                return true;
        }

        return false;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
                break;

            count++;
            index = found + value.Length;
        }

        return count;
    }
}
