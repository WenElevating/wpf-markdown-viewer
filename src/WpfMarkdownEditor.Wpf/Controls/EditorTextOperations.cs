namespace WpfMarkdownEditor.Wpf.Controls;

internal readonly record struct TextEditOperation(string Text, int SelectionStart, int SelectionLength);

internal static class EditorTextOperations
{
    public static TextEditOperation InsertText(string text, int selectionStart, int selectionLength, string insertion)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);
        var updated = text.Remove(start, length).Insert(start, insertion);
        return new TextEditOperation(updated, start + insertion.Length, 0);
    }

    public static TextEditOperation? DeleteSelectionOrCurrentLine(
        string text,
        int selectionStart,
        int selectionLength)
    {
        if (selectionLength > 0)
            return InsertText(text, selectionStart, selectionLength, string.Empty);

        if (text.Length == 0)
            return null;

        var document = LineDocument.Parse(text);
        if (document.Lines.Count == 0)
            return null;

        var lineIndex = document.GetLineIndex(selectionStart, isSelectionEnd: false);
        var lines = document.Lines.Select(line => line.Content).ToList();
        lines.RemoveAt(lineIndex);

        var updated = document.BuildText(lines);
        var caret = Math.Min(document.GetLineStart(lineIndex), updated.Length);
        return new TextEditOperation(updated, caret, 0);
    }

    public static TextEditOperation? MoveSelectedLines(
        string text,
        int selectionStart,
        int selectionLength,
        int direction)
    {
        if (direction != -1 && direction != 1)
            throw new ArgumentOutOfRangeException(nameof(direction));

        if (text.Length == 0)
            return null;

        var document = LineDocument.Parse(text);
        if (document.Lines.Count == 0)
            return null;

        var startLine = document.GetLineIndex(selectionStart, isSelectionEnd: false);
        var endLine = selectionLength == 0
            ? startLine
            : document.GetLineIndex(selectionStart + selectionLength, isSelectionEnd: true);

        return direction < 0
            ? MoveBlockUp(document, startLine, endLine)
            : MoveBlockDown(document, startLine, endLine);
    }

    private static TextEditOperation? MoveBlockUp(LineDocument document, int startLine, int endLine)
    {
        if (startLine <= 0)
            return null;

        var lines = document.Lines.Select(line => line.Content).ToList();
        var previous = lines[startLine - 1];
        lines.RemoveAt(startLine - 1);
        lines.Insert(endLine, previous);

        var newStartLine = startLine - 1;
        return new TextEditOperation(
            document.BuildText(lines),
            document.GetLineStart(lines, newStartLine),
            document.GetSelectionLength(lines, newStartLine, endLine - 1));
    }

    private static TextEditOperation? MoveBlockDown(LineDocument document, int startLine, int endLine)
    {
        if (endLine >= document.Lines.Count - 1)
            return null;

        var lines = document.Lines.Select(line => line.Content).ToList();
        var next = lines[endLine + 1];
        lines.RemoveAt(endLine + 1);
        lines.Insert(startLine, next);

        var newStartLine = startLine + 1;
        return new TextEditOperation(
            document.BuildText(lines),
            document.GetLineStart(lines, newStartLine),
            document.GetSelectionLength(lines, newStartLine, endLine + 1));
    }

    private sealed record LineInfo(string Content, int Start);

    private sealed class LineDocument
    {
        private LineDocument(IReadOnlyList<LineInfo> lines, string newline, bool hasTrailingNewline)
        {
            Lines = lines;
            Newline = newline;
            HasTrailingNewline = hasTrailingNewline;
        }

        public IReadOnlyList<LineInfo> Lines { get; }
        private string Newline { get; }
        private bool HasTrailingNewline { get; }

        public static LineDocument Parse(string text)
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

            if (start < text.Length)
                lines.Add(new LineInfo(text[start..], start));

            var hasTrailingNewline = text.EndsWith("\n", StringComparison.Ordinal);
            return new LineDocument(lines, newline, hasTrailingNewline);
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

        public int GetLineStart(int lineIndex)
        {
            return GetLineStart(Lines.Select(line => line.Content).ToList(), lineIndex);
        }

        public int GetLineStart(IReadOnlyList<string> lines, int lineIndex)
        {
            var index = Math.Clamp(lineIndex, 0, Math.Max(0, lines.Count - 1));
            var start = 0;
            for (var i = 0; i < index; i++)
                start += lines[i].Length + Newline.Length;

            return start;
        }

        public int GetSelectionLength(int startLine, int endLine)
        {
            return GetSelectionLength(Lines.Select(line => line.Content).ToList(), startLine, endLine);
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
