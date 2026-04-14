using WpfMarkdownEditor.Core.Parsing.Inlines;

namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Parses inline Markdown elements using delimiter stack algorithm.
/// </summary>
internal sealed class InlineParser
{
    public List<Inline> ParseInlines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [new TextInline { Content = "" }];

        var inlines = new List<Inline>();
        var i = 0;

        while (i < text.Length)
        {
            var consumed = TryParseInline(text, i, out var inline);
            if (consumed > 0 && inline is not null)
            {
                inlines.Add(inline);
                i += consumed;
            }
            else
            {
                // Accumulate plain text
                var start = i;
                while (i < text.Length && !IsSpecialChar(text[i]))
                    i++;

                if (i > start)
                    inlines.Add(new TextInline { Content = text[start..i] });
                else
                {
                    // Single special char treated as text
                    inlines.Add(new TextInline { Content = text[i..(i + 1)] });
                    i++;
                }
            }
        }

        // Process emphasis: merge **, *, __, _ into Bold, Italic, BoldItalic
        return ProcessEmphasis(inlines);
    }

    private static bool IsSpecialChar(char c) =>
        c is '*' or '_' or '`' or '[' or '!' or '~' or '\\' or '\n';

    private int TryParseInline(string text, int start, out Inline? inline)
    {
        inline = null;
        var c = text[start];

        return c switch
        {
            '\\' => TryParseEscape(text, start, out inline),
            '`' => TryParseCodeSpan(text, start, out inline),
            '!' => TryParseImage(text, start, out inline),
            '[' => TryParseLink(text, start, out inline),
            '~' => TryParseStrikethrough(text, start, out inline),
            '\n' => TryParseLineBreak(text, start, out inline),
            '*' or '_' => TryParseEmphasisMarker(text, start, out inline),
            _ => 0
        };
    }

    #region Escape

    private static int TryParseEscape(string text, int start, out Inline? inline)
    {
        inline = null;
        if (start + 1 >= text.Length) return 0;

        var next = text[start + 1];
        // CommonMark escapable characters
        if (next is '!' or '"' or '#' or '$' or '%' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or '-' or '.' or '/' or ':' or ';' or '<' or '=' or '>' or '?' or '@' or '[' or '\\' or ']' or '^' or '_' or '`' or '{' or '|' or '}' or '~')
        {
            inline = new TextInline { Content = next.ToString() };
            return 2;
        }

        return 0;
    }

    #endregion

    #region Code Span

    private static int TryParseCodeSpan(string text, int start, out Inline? inline)
    {
        inline = null;
        // Count opening backticks
        var openLen = 0;
        while (start + openLen < text.Length && text[start + openLen] == '`')
            openLen++;

        var closeIdx = text.IndexOf(new string('`', openLen), start + openLen, StringComparison.Ordinal);
        if (closeIdx < 0) return 0;

        var code = text[(start + openLen)..closeIdx];
        // Strip one leading and trailing space if both exist
        if (code.Length > 0 && code[0] == ' ' && code[^1] == ' ')
            code = code[1..^1];

        inline = new CodeInline { Code = code.Replace('\n', ' ') };
        return closeIdx + openLen - start;
    }

    #endregion

    #region Links

    private int TryParseLink(string text, int start, out Inline? inline)
    {
        inline = null;
        // Find closing ]
        var closeBracket = FindCloseBracket(text, start);
        if (closeBracket < 0) return 0;

        // Check for ( after ]
        if (closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
            return 0;

        // Find closing )
        var closeParen = FindCloseParen(text, closeBracket + 2);
        if (closeParen < 0) return 0;

        var linkText = text[(start + 1)..closeBracket];
        var urlAndTitle = text[(closeBracket + 2)..closeParen].Trim();

        // Parse URL (strip title if present)
        var url = ExtractUrl(urlAndTitle);
        var title = ExtractTitle(urlAndTitle);

        var children = ParseInlines(linkText);

        inline = new LinkInline
        {
            Url = url,
            Title = title,
            Children = children
        };
        return closeParen + 1 - start;
    }

    #endregion

    #region Image

    private int TryParseImage(string text, int start, out Inline? inline)
    {
        inline = null;
        if (start + 1 >= text.Length || text[start + 1] != '[') return 0;

        var closeBracket = FindCloseBracket(text, start + 1);
        if (closeBracket < 0) return 0;

        if (closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
            return 0;

        var closeParen = FindCloseParen(text, closeBracket + 2);
        if (closeParen < 0) return 0;

        var alt = text[(start + 2)..closeBracket];
        var urlAndTitle = text[(closeBracket + 2)..closeParen].Trim();

        inline = new ImageInline
        {
            Alt = alt,
            Url = ExtractUrl(urlAndTitle),
            Title = ExtractTitle(urlAndTitle)
        };
        return closeParen + 1 - start;
    }

    #endregion

    #region Strikethrough

    private static int TryParseStrikethrough(string text, int start, out Inline? inline)
    {
        inline = null;
        // Must be ~~
        if (start + 1 >= text.Length || text[start + 1] != '~') return 0;

        // Find closing ~~
        var closeIdx = text.IndexOf("~~", start + 2, StringComparison.Ordinal);
        if (closeIdx < 0) return 0;

        var content = text[(start + 2)..closeIdx];
        if (content.Length == 0) return 0;

        // Parse inner inlines by creating a temporary parser
        var innerParser = new InlineParser();
        var children = innerParser.ParseInlines(content);

        inline = new StrikethroughInline { Children = children };
        return closeIdx + 2 - start;
    }

    #endregion

    #region Line Break

    private static int TryParseLineBreak(string text, int start, out Inline? inline)
    {
        inline = new LineBreakInline();
        return 1;
    }

    #endregion

    #region Emphasis Markers

    private static int TryParseEmphasisMarker(string text, int start, out Inline? inline)
    {
        inline = null;
        var c = text[start];
        var count = 0;
        while (start + count < text.Length && text[start + count] == c)
            count++;

        // Treat as a text run of the marker characters; emphasis is resolved in ProcessEmphasis
        inline = new TextInline { Content = new string(c, count) };
        inline.SourceOffset = start;
        return count;
    }

    #endregion

    #region Emphasis Processing

    private static List<Inline> ProcessEmphasis(List<Inline> inlines)
    {
        var textRuns = new List<(int index, TextInline run)>();

        // Collect all text runs that are emphasis markers
        for (var i = 0; i < inlines.Count; i++)
        {
            if (inlines[i] is TextInline ti && ti.Content.Length > 0 &&
                ti.Content.All(c => c == '*' || c == '_'))
            {
                textRuns.Add((i, ti));
            }
        }

        // No emphasis markers — merge and return
        if (textRuns.Count == 0)
            return MergeAdjacentText(inlines);

        // Track which indices are consumed by emphasis (including inner content)
        var consumed = new HashSet<int>();
        // Track ordered results with their start position for sorting
        var results = new List<(int position, Inline inline)>();

        // Process pairs: opening then closing
        for (var i = 0; i < textRuns.Count; i++)
        {
            if (consumed.Contains(textRuns[i].index)) continue;

            var (openIdx, openRun) = textRuns[i];
            var openChar = openRun.Content[0];

            // Find matching closing delimiter
            for (var j = i + 1; j < textRuns.Count; j++)
            {
                if (consumed.Contains(textRuns[j].index)) continue;

                var (closeIdx, closeRun) = textRuns[j];
                if (closeRun.Content[0] != openChar) continue;

                var openLen = openRun.Content.Length;
                var closeLen = closeRun.Content.Length;

                // Determine emphasis type
                if (openLen >= 2 && closeLen >= 2)
                {
                    // Bold (or BoldItalic if both >= 3)
                    var innerStart = openIdx + 1;
                    var innerEnd = closeIdx;

                    var innerInlines = inlines[innerStart..innerEnd];
                    innerInlines = ProcessEmphasis(innerInlines.ToList());

                    Inline emphasis = new BoldInline { Children = innerInlines };

                    // If both have 3+ markers, wrap in BoldItalic
                    if (openLen >= 3 && closeLen >= 3)
                    {
                        emphasis = new BoldItalicInline { Children = [emphasis] };
                    }

                    // Consume opening marker, inner content, and closing marker
                    consumed.Add(openIdx);
                    for (var k = innerStart; k < innerEnd; k++) consumed.Add(k);
                    consumed.Add(closeIdx);

                    results.Add((openIdx, emphasis));
                    break;
                }
                else if (openLen >= 1 && closeLen >= 1)
                {
                    // Italic
                    var innerStart = openIdx + 1;
                    var innerEnd = closeIdx;
                    var innerInlines = inlines[innerStart..innerEnd];
                    innerInlines = ProcessEmphasis(innerInlines.ToList());

                    var italic = new ItalicInline { Children = innerInlines };

                    // Consume opening marker, inner content, and closing marker
                    consumed.Add(openIdx);
                    for (var k = innerStart; k < innerEnd; k++) consumed.Add(k);
                    consumed.Add(closeIdx);

                    results.Add((openIdx, italic));
                    break;
                }
            }
        }

        // Rebuild: interleave non-consumed inlines and emphasis results in order
        var output = new List<Inline>();
        var consumedResultIndices = new HashSet<int>();

        for (var i = 0; i < inlines.Count; i++)
        {
            if (consumed.Contains(i)) continue;

            // Check if any emphasis result should be inserted here
            for (var r = 0; r < results.Count; r++)
            {
                if (!consumedResultIndices.Contains(r) && results[r].position == i)
                {
                    output.Add(results[r].inline);
                    consumedResultIndices.Add(r);
                }
            }

            output.Add(inlines[i]);
        }

        // Add any remaining emphasis results at the end
        for (var r = 0; r < results.Count; r++)
        {
            if (!consumedResultIndices.Contains(r))
                output.Add(results[r].inline);
        }

        return MergeAdjacentText(output);
    }

    private static List<Inline> MergeAdjacentText(List<Inline> inlines)
    {
        var result = new List<Inline>();
        var textBuffer = new StringBuilder();

        foreach (var inline in inlines)
        {
            if (inline is TextInline ti)
            {
                textBuffer.Append(ti.Content);
            }
            else
            {
                if (textBuffer.Length > 0)
                {
                    result.Add(new TextInline { Content = textBuffer.ToString() });
                    textBuffer.Clear();
                }
                result.Add(inline);
            }
        }

        if (textBuffer.Length > 0)
            result.Add(new TextInline { Content = textBuffer.ToString() });

        return result;
    }

    #endregion

    #region Helpers

    private static int FindCloseBracket(string text, int start)
    {
        var depth = 0;
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == '[') depth++;
            if (text[i] == ']')
            {
                if (depth == 0) return i;
                depth--;
            }
        }
        return -1;
    }

    private static int FindCloseParen(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == '(') depth++;
            if (text[i] == ')')
            {
                if (depth == 0) return i;
                depth--;
            }
        }
        return -1;
    }

    private static string ExtractUrl(string urlAndTitle)
    {
        // URL can't contain spaces unless in angle brackets
        if (urlAndTitle.Length == 0) return "";

        if (urlAndTitle[0] == '<')
        {
            var close = urlAndTitle.IndexOf('>');
            if (close >= 0) return urlAndTitle[1..close];
        }

        var spaceIdx = urlAndTitle.IndexOf(' ');
        return spaceIdx < 0 ? urlAndTitle : urlAndTitle[..spaceIdx];
    }

    private static string? ExtractTitle(string urlAndTitle)
    {
        var spaceIdx = urlAndTitle.IndexOf(' ');
        if (spaceIdx < 0) return null;

        var rest = urlAndTitle[(spaceIdx + 1)..].Trim();
        if (rest.Length < 2) return null;

        var quoteChar = rest[0];
        if (quoteChar is not ('"' or '\'')) return null;

        var closeIdx = rest.LastIndexOf(quoteChar);
        if (closeIdx <= 0) return null;

        return rest[1..closeIdx];
    }

    #endregion
}
