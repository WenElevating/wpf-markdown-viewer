using System.Text.RegularExpressions;

namespace WpfMarkdownEditor.Wpf.Controls;

internal static class MarkdownParagraphOperations
{
    private static readonly Regex BlockPrefixPattern = new(
        @"^(?<prefix>#{1,6}|>|[-*+]|\d+\.)\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextEditOperation SetHeadingLevel(string text, int selectionStart, int selectionLength, int level)
    {
        if (level < 1 || level > 6)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Heading level must be between 1 and 6.");

        return TransformSelectedLines(
            text,
            selectionStart,
            selectionLength,
            line => AddPrefix(RemoveBlockPrefix(line), new string('#', level)));
    }

    public static TextEditOperation ClearBlockPrefix(string text, int selectionStart, int selectionLength) =>
        TransformSelectedLines(text, selectionStart, selectionLength, RemoveBlockPrefix);

    public static TextEditOperation ToggleBlockquote(string text, int selectionStart, int selectionLength) =>
        TogglePrefix(text, selectionStart, selectionLength, ">");

    public static TextEditOperation ToggleOrderedList(string text, int selectionStart, int selectionLength) =>
        TogglePrefix(text, selectionStart, selectionLength, "1.");

    public static TextEditOperation ToggleBulletList(string text, int selectionStart, int selectionLength) =>
        TogglePrefix(text, selectionStart, selectionLength, "-");

    public static TextEditOperation InsertParagraphAbove(string text, int selectionStart, int selectionLength)
    {
        var document = ParagraphLineDocument.Parse(text);
        var range = document.GetSelectedLineRange(selectionStart, selectionLength);
        var insertAt = document.GetLineStart(range.StartLine);
        var updated = text.Insert(insertAt, document.Newline);
        return new TextEditOperation(updated, insertAt, 0);
    }

    public static TextEditOperation InsertParagraphBelow(string text, int selectionStart, int selectionLength)
    {
        var document = ParagraphLineDocument.Parse(text);
        var range = document.GetSelectedLineRange(selectionStart, selectionLength);
        var insertAt = document.GetLineEnd(range.EndLine);
        var updated = text.Insert(insertAt, document.Newline);
        return new TextEditOperation(updated, insertAt + document.Newline.Length, 0);
    }

    public static TextEditOperation InsertHorizontalRule(string text, int selectionStart, int selectionLength)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);
        var newline = DetectNewline(text);
        var before = text[..start];
        var after = text[(start + length)..];
        var insertion = GetBlockPrefix(before, newline) + "---" + GetBlockSuffix(after, newline);
        return EditorTextOperations.InsertText(text, start, length, insertion);
    }

    private static TextEditOperation TogglePrefix(string text, int selectionStart, int selectionLength, string prefix) =>
        TransformSelectedLines(
            text,
            selectionStart,
            selectionLength,
            line =>
            {
                var match = BlockPrefixPattern.Match(line);
                if (match.Success && string.Equals(match.Groups["prefix"].Value, prefix, StringComparison.Ordinal))
                    return line[match.Length..];

                return AddPrefix(RemoveBlockPrefix(line), prefix);
            });

    private static TextEditOperation TransformSelectedLines(
        string text,
        int selectionStart,
        int selectionLength,
        Func<string, string> transform)
    {
        var document = ParagraphLineDocument.Parse(text);
        var range = document.GetSelectedLineRange(selectionStart, selectionLength);
        var lines = document.Lines.Select(line => line.Content).ToList();

        for (var i = range.StartLine; i <= range.EndLine; i++)
            lines[i] = transform(lines[i]);

        var updated = document.BuildText(lines);
        var newSelectionStart = document.GetLineStart(lines, range.StartLine);
        var newSelectionLength = selectionLength == 0
            ? 0
            : document.GetSelectionLength(lines, range.StartLine, range.EndLine);

        if (selectionLength == 0)
        {
            var originalLineStart = document.GetLineStart(range.StartLine);
            var originalColumn = Math.Max(0, Math.Clamp(selectionStart, 0, text.Length) - originalLineStart);
            var originalLine = document.Lines[range.StartLine].Content;
            var updatedLine = lines[range.StartLine];
            var adjustedColumn = originalColumn + GetPrefixLength(updatedLine) - GetPrefixLength(originalLine);
            newSelectionStart += Math.Clamp(adjustedColumn, 0, updatedLine.Length);
        }

        return new TextEditOperation(updated, newSelectionStart, newSelectionLength);
    }

    private static string RemoveBlockPrefix(string line)
    {
        var match = BlockPrefixPattern.Match(line);
        return match.Success ? line[match.Length..] : line;
    }

    private static string AddPrefix(string line, string prefix) =>
        string.IsNullOrWhiteSpace(line) ? prefix + " " : prefix + " " + line;

    private static int GetPrefixLength(string line)
    {
        var match = BlockPrefixPattern.Match(line);
        return match.Success ? match.Length : 0;
    }

    private static string DetectNewline(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal))
            return "\r\n";

        return text.Contains('\n', StringComparison.Ordinal)
            ? "\n"
            : Environment.NewLine;
    }

    private static string GetBlockPrefix(string before, string newline)
    {
        if (before.Length == 0)
            return string.Empty;

        if (before.EndsWith(newline + newline, StringComparison.Ordinal))
            return string.Empty;

        if (before.EndsWith(newline, StringComparison.Ordinal))
            return newline;

        return newline + newline;
    }

    private static string GetBlockSuffix(string after, string newline)
    {
        if (after.Length == 0)
            return string.Empty;

        if (after.StartsWith(newline + newline, StringComparison.Ordinal))
            return string.Empty;

        if (after.StartsWith(newline, StringComparison.Ordinal))
            return newline;

        return newline + newline;
    }

    private sealed record LineRange(int StartLine, int EndLine);
    private sealed record LineInfo(string Content, int Start);

    private sealed class ParagraphLineDocument
    {
        private ParagraphLineDocument(IReadOnlyList<LineInfo> lines, string newline, bool hasTrailingNewline)
        {
            Lines = lines;
            Newline = newline;
            HasTrailingNewline = hasTrailingNewline;
        }

        public IReadOnlyList<LineInfo> Lines { get; }
        public string Newline { get; }
        private bool HasTrailingNewline { get; }

        public static ParagraphLineDocument Parse(string text)
        {
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = new List<LineInfo>();
            var start = 0;
            var index = 0;

            while (index < text.Length)
            {
                if (text[index] == '\r' || text[index] == '\n')
                {
                    lines.Add(new LineInfo(text[start..index], start));
                    if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;

                    index++;
                    start = index;
                    continue;
                }

                index++;
            }

            if (start < text.Length || text.Length == 0)
                lines.Add(new LineInfo(text[start..], start));

            return new ParagraphLineDocument(lines, newline, text.EndsWith("\n", StringComparison.Ordinal));
        }

        public LineRange GetSelectedLineRange(int selectionStart, int selectionLength)
        {
            var start = Math.Clamp(selectionStart, 0, GetOriginalLength());
            var length = Math.Clamp(selectionLength, 0, GetOriginalLength() - start);
            var startLine = GetLineIndex(start, isSelectionEnd: false);
            var endLine = length == 0
                ? startLine
                : GetLineIndex(start + length, isSelectionEnd: true);
            return new LineRange(startLine, endLine);
        }

        public int GetLineIndex(int position, bool isSelectionEnd)
        {
            if (Lines.Count == 0)
                return 0;

            var clamped = Math.Clamp(position, 0, GetOriginalLength());
            for (var i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Start == clamped && isSelectionEnd && clamped > 0)
                    return Math.Max(0, i - 1);

                var nextStart = i + 1 < Lines.Count ? Lines[i + 1].Start : int.MaxValue;
                if (clamped < nextStart)
                    return i;
            }

            return Lines.Count - 1;
        }

        public int GetLineStart(int lineIndex) => GetLineStart(Lines.Select(line => line.Content).ToList(), lineIndex);

        public int GetLineStart(IReadOnlyList<string> lines, int lineIndex)
        {
            var index = Math.Clamp(lineIndex, 0, Math.Max(0, lines.Count - 1));
            var start = 0;
            for (var i = 0; i < index; i++)
                start += lines[i].Length + Newline.Length;

            return start;
        }

        public int GetLineEnd(int lineIndex)
        {
            var index = Math.Clamp(lineIndex, 0, Math.Max(0, Lines.Count - 1));
            return Lines[index].Start + Lines[index].Content.Length;
        }

        public int GetSelectionLength(IReadOnlyList<string> lines, int startLine, int endLine)
        {
            var length = 0;
            for (var i = startLine; i <= endLine; i++)
            {
                if (i > startLine)
                    length += Newline.Length;

                length += lines[i].Length;
            }

            return length;
        }

        public string BuildText(IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
                return string.Empty;

            var text = string.Join(Newline, lines);
            return HasTrailingNewline ? text + Newline : text;
        }

        private int GetOriginalLength()
        {
            if (Lines.Count == 0)
                return 0;

            var length = Lines.Sum(line => line.Content.Length);
            length += Math.Max(0, Lines.Count - 1) * Newline.Length;
            if (HasTrailingNewline)
                length += Newline.Length;

            return length;
        }
    }
}
