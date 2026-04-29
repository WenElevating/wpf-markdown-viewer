using System.Text;
using System.Text.RegularExpressions;

namespace WpfMarkdownEditor.Core.Translation;

/// <summary>
/// Template-based markdown extraction for translation.
/// Parses markdown into a structured template, extracts plain translatable text,
/// and reconstructs the markdown after translation with all formatting preserved.
///
/// Strategy:
/// - Line-level structure (headings, lists, blockquotes, tables, code blocks, HR)
///   is captured in a template and reapplied after translation.
/// - Inline markers (bold, italic, code, links) are replaced with ASCII tokens
///   that survive translation APIs, then restored after translation.
/// - Code blocks are fully preserved (not sent for translation).
/// </summary>
public static class MarkdownSegmentExtractor
{
    // --- Inline marker patterns ---
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`([^`\n]+)`", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex ImageRegex = new(@"!\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex StandaloneUrlRegex = new(@"(?<!\()(?:https?|ftp)://[^\s<>)""]+", RegexOptions.Compiled);

    // --- Line-level structure patterns ---
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+", RegexOptions.Compiled);
    private static readonly Regex BlockquoteRegex = new(@"^(>\s?)", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^([-*+])\s+", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^(\d+\.)\s+", RegexOptions.Compiled);
    private static readonly Regex HorizontalRuleRegex = new(@"^\s*(---+|\*\*\*+|___+)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// A segment in the markdown template representing either a preserved element
    /// or a translatable text line.
    /// </summary>
    public record Segment(
        string Type,       // "preserve", "blank", "text", "table"
        string? Raw = null,
        string? Prefix = null,
        int CellCount = 0
    );

    /// <summary>
    /// Extracts translatable text from markdown using a template-based approach.
    /// Returns: (plain text for translation, template for reconstruction, inline token map).
    /// </summary>
    public static (string plainText, List<Segment> template, Dictionary<string, string> inlineTokens)
        Extract(string markdown)
    {
        var template = new List<Segment>();
        var textLines = new List<string>();
        var inlineTokens = new Dictionary<string, string>();
        var counters = new Dictionary<string, int>
        {
            ["B"] = 0, ["I"] = 0, ["C"] = 0, ["L"] = 0, ["U"] = 0
        };

        var inCodeBlock = false;
        var codeBlockLines = new List<string>();

        // Handle empty input
        if (string.IsNullOrWhiteSpace(markdown))
            return ("", [], new Dictionary<string, string>());

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            // --- Code block handling ---
            if (rawLine.TrimStart().StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeBlockLines = [rawLine];
                }
                else
                {
                    codeBlockLines.Add(rawLine);
                    template.Add(new Segment("preserve", Raw: string.Join("\n", codeBlockLines)));
                    codeBlockLines = [];
                    inCodeBlock = false;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(rawLine);
                continue;
            }

            // --- Blank lines ---
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                template.Add(new Segment("blank"));
                continue;
            }

            // --- Horizontal rules ---
            if (HorizontalRuleRegex.IsMatch(rawLine))
            {
                template.Add(new Segment("preserve", Raw: rawLine));
                continue;
            }

            // --- Table separator rows ---
            if (IsTableSeparator(rawLine))
            {
                template.Add(new Segment("preserve", Raw: rawLine));
                continue;
            }

            // --- Table content rows ---
            if (rawLine.TrimStart().StartsWith("|"))
            {
                var cells = ParseTableCells(rawLine);
                var tokenizedCells = cells.Select(c => TokenizeInline(c, inlineTokens, counters)).ToList();
                template.Add(new Segment("table", CellCount: tokenizedCells.Count));
                textLines.AddRange(tokenizedCells);
                continue;
            }

            // --- Text lines (headings, lists, blockquotes, paragraphs) ---
            var (prefix, content) = ExtractLinePrefix(rawLine);
            var tokenized = TokenizeInline(content, inlineTokens, counters);
            template.Add(new Segment("text", Prefix: prefix));
            textLines.Add(tokenized);
        }

        // Handle unclosed code block
        if (inCodeBlock && codeBlockLines.Count > 0)
            template.Add(new Segment("preserve", Raw: string.Join("\n", codeBlockLines)));

        return (string.Join("\n", textLines), template, inlineTokens);
    }

    /// <summary>
    /// Reconstructs markdown from the template, translated text, and inline tokens.
    /// </summary>
    public static string Reconstruct(
        List<Segment> template,
        string translatedText,
        Dictionary<string, string> inlineTokens)
    {
        var lines = translatedText.Replace("\r\n", "\n").Split('\n');
        var idx = 0;
        var sb = new StringBuilder();

        foreach (var seg in template)
        {
            switch (seg.Type)
            {
                case "preserve":
                    sb.Append(seg.Raw).Append('\n');
                    break;

                case "blank":
                    sb.Append('\n');
                    break;

                case "table":
                    var cells = new List<string>();
                    for (var i = 0; i < seg.CellCount && idx < lines.Length; i++)
                        cells.Add(lines[idx++].Trim());
                    sb.Append("| " + string.Join(" | ", cells) + " |").Append('\n');
                    break;

                case "text":
                    var translated = idx < lines.Length ? lines[idx++].Trim() : "";
                    sb.Append(seg.Prefix + translated).Append('\n');
                    break;
            }
        }

        var result = sb.ToString().TrimEnd('\n');
        return RestoreInlineTokens(result, inlineTokens);
    }

    #region Line prefix extraction

    private static (string prefix, string content) ExtractLinePrefix(string line)
    {
        var m = HeadingRegex.Match(line);
        if (m.Success) return (m.Groups[1].Value + " ", line[m.Length..]);

        m = BlockquoteRegex.Match(line);
        if (m.Success) return (m.Groups[1].Value, line[m.Length..]);

        m = UnorderedListRegex.Match(line);
        if (m.Success) return (m.Groups[1].Value + " ", line[m.Length..]);

        m = OrderedListRegex.Match(line);
        if (m.Success) return (m.Groups[1].Value + " ", line[m.Length..]);

        return ("", line);
    }

    #endregion

    #region Inline tokenization

    private static string TokenizeInline(
        string text,
        Dictionary<string, string> tokens,
        Dictionary<string, int> counters)
    {
        // Order: images → links → inline code → bold → italic → standalone URLs
        // Images and links first to avoid partial matches.

        // Images: ![alt](url) → preserve syntax, replace URL with token
        text = ImageRegex.Replace(text, match =>
        {
            var idx = counters["U"]++;
            var token = $"XURL{idx}";
            tokens[token] = match.Groups[2].Value;
            return $"![{match.Groups[1].Value}]({token})";
        });

        // Links: [text](url) → XLS_N text XLE_N (closing token stores URL)
        text = LinkRegex.Replace(text, match =>
        {
            var idx = counters["L"]++;
            var startToken = $"XLS{idx}";
            var endToken = $"XLE{idx}";
            tokens[startToken] = "[";
            tokens[endToken] = $"]({match.Groups[2].Value})";
            return $"{startToken}{match.Groups[1].Value}{endToken}";
        });

        // Inline code: `text` → XC_N
        text = InlineCodeRegex.Replace(text, match =>
        {
            var idx = counters["C"]++;
            var token = $"XC{idx}";
            tokens[token] = match.Value;
            return token;
        });

        // Bold: **text** → XBS_N text XBE_N
        text = BoldRegex.Replace(text, match =>
        {
            var idx = counters["B"]++;
            var startToken = $"XBS{idx}";
            var endToken = $"XBE{idx}";
            tokens[startToken] = "**";
            tokens[endToken] = "**";
            return $"{startToken}{match.Groups[1].Value}{endToken}";
        });

        // Italic: *text* → XIS_N text XIE_N
        text = ItalicRegex.Replace(text, match =>
        {
            var idx = counters["I"]++;
            var startToken = $"XIS{idx}";
            var endToken = $"XIE{idx}";
            tokens[startToken] = "*";
            tokens[endToken] = "*";
            return $"{startToken}{match.Groups[1].Value}{endToken}";
        });

        // Standalone URLs
        text = StandaloneUrlRegex.Replace(text, match =>
        {
            var idx = counters["U"]++;
            var token = $"XURL{idx}";
            tokens[token] = match.Value;
            return token;
        });

        return text;
    }

    private static string RestoreInlineTokens(string text, Dictionary<string, string> tokens)
    {
        // Exact token replacement
        foreach (var (token, marker) in tokens)
            text = text.Replace(token, marker);

        // Regex fallback: if the translation API partially modified a token
        // (e.g., added spaces, changed case), pattern-match by structure.
        text = Regex.Replace(text, @"XBS\d+", "**");
        text = Regex.Replace(text, @"XBE\d+", "**");
        text = Regex.Replace(text, @"XIS\d+", "*");
        text = Regex.Replace(text, @"XIE\d+", "*");
        text = Regex.Replace(text, @"XLS\d+", "[");
        // XLE tokens contain URLs — only restore via exact match above

        return text;
    }

    #endregion

    #region Table helpers

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("|")) return false;
        var cells = trimmed.Split('|').Where(c => !string.IsNullOrWhiteSpace(c.Trim()));
        return cells.All(c => Regex.IsMatch(c.Trim(), @"^[\s\-:]+$"));
    }

    private static List<string> ParseTableCells(string line)
    {
        var cells = line.Split('|')
            .Skip(1)
            .ToList();

        if (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[^1]))
            cells.RemoveAt(cells.Count - 1);

        return cells
            .Select(c => c.Trim())
            .Where(c => string.IsNullOrWhiteSpace(c) || !Regex.IsMatch(c, @"^[\s\-:]+$"))
            .ToList();
    }

    #endregion
}
