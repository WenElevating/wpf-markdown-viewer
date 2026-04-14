namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Enumerates lines of text with position tracking for source mapping.
/// </summary>
internal sealed class LineReader
{
    private readonly string _text;
    private int _position;

    public LineReader(string text)
    {
        _text = text ?? string.Empty;
        _position = 0;
    }

    public int CurrentLine { get; private set; } = 1;

    public bool HasMore => _position < _text.Length;

    /// <summary>
    /// Reads the next line including the line ending. Returns null at end.
    /// </summary>
    public LineInfo? ReadLine()
    {
        if (_position >= _text.Length)
            return null;

        var start = _position;
        var lineStart = CurrentLine;

        while (_position < _text.Length && _text[_position] != '\n' && _text[_position] != '\r')
            _position++;

        var end = _position;
        var content = _text[start..end];

        // Consume line ending
        if (_position < _text.Length)
        {
            if (_text[_position] == '\r')
            {
                _position++;
                if (_position < _text.Length && _text[_position] == '\n')
                    _position++;
            }
            else if (_text[_position] == '\n')
            {
                _position++;
            }
        }

        CurrentLine++;
        return new LineInfo(content, lineStart, start);
    }

    /// <summary>
    /// Peek at the next line without advancing.
    /// </summary>
    public LineInfo? PeekLine()
    {
        var savedPos = _position;
        var savedLine = CurrentLine;
        var result = ReadLine();
        _position = savedPos;
        CurrentLine = savedLine;
        return result;
    }
}

internal sealed class LineInfo(string content, int lineNumber, int offset)
{
    public string Content { get; } = content;
    public int LineNumber { get; } = lineNumber;
    public int Offset { get; } = offset;

    /// <summary>
    /// Content with up to 3 spaces of indentation stripped (per CommonMark).
    /// </summary>
    public string StrippedContent()
    {
        var span = Content.AsSpan();
        var stripped = 0;
        while (stripped < 3 && stripped < span.Length && span[stripped] == ' ')
            stripped++;
        return Content[stripped..];
    }

    public int IndentLevel
    {
        get
        {
            var count = 0;
            while (count < Content.Length && count < 3 && Content[count] == ' ')
                count++;
            return count;
        }
    }
}
