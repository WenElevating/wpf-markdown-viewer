using System.Net;

namespace WpfMarkdownEditor.Core.Parsing.Html;

internal sealed class HtmlTokenizer(string input)
{
    private readonly string _input = input ?? string.Empty;
    private int _pos;

    public IEnumerable<HtmlToken> Tokenize()
    {
        while (_pos < _input.Length)
        {
            if (Peek() == '<')
            {
                if (TrySkipCommentOrDeclaration())
                    continue;

                yield return ReadTagOrText();
            }
            else
            {
                yield return ReadText();
            }
        }
    }

    private HtmlToken ReadTagOrText()
    {
        var start = _pos;
        Advance();

        if (_pos >= _input.Length)
            return ReadMalformedTagAsText(start);

        if (Peek() == '/')
        {
            Advance();
            var closeName = ReadName();
            if (closeName.Length == 0 || !SkipUntilTagEnd())
                return ReadMalformedTagAsText(start);

            return new HtmlToken(HtmlTokenKind.CloseTag, closeName.ToLowerInvariant(), HtmlToken.EmptyAttributes, null);
        }

        var name = ReadName();
        if (name.Length == 0)
            return ReadMalformedTagAsText(start);

        var attrs = ReadAttributes();
        SkipWhitespace();
        var explicitSelfClose = PeekOrNull() == '/';
        if (!SkipUntilTagEnd())
            return ReadMalformedTagAsText(start);

        var normalized = name.ToLowerInvariant();
        var kind = explicitSelfClose || IsVoidElement(normalized)
            ? HtmlTokenKind.SelfClose
            : HtmlTokenKind.OpenTag;

        return new HtmlToken(kind, normalized, attrs, null);
    }

    private HtmlToken ReadText()
    {
        var start = _pos;
        while (_pos < _input.Length && Peek() != '<')
            Advance();

        return new HtmlToken(HtmlTokenKind.Text, string.Empty, HtmlToken.EmptyAttributes, DecodeEntities(_input[start.._pos]));
    }

    private HtmlToken ReadMalformedTagAsText(int start)
    {
        _pos = start;
        var text = _input[start..];
        _pos = _input.Length;
        return new HtmlToken(HtmlTokenKind.Text, string.Empty, HtmlToken.EmptyAttributes, DecodeEntities(text));
    }

    private Dictionary<string, string> ReadAttributes()
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (_pos < _input.Length)
        {
            SkipWhitespace();
            var current = PeekOrNull();
            if (current is null or '>' or '/')
                break;

            var key = ReadName();
            if (key.Length == 0)
            {
                Advance();
                continue;
            }

            SkipWhitespace();
            var value = string.Empty;
            if (PeekOrNull() == '=')
            {
                Advance();
                SkipWhitespace();
                value = ReadAttributeValue();
            }

            attrs[key] = value;
        }

        return attrs;
    }

    private string ReadAttributeValue()
    {
        var quote = PeekOrNull();
        if (quote is '"' or '\'')
        {
            Advance();
            var start = _pos;
            while (_pos < _input.Length && Peek() != quote)
                Advance();

            var value = _input[start.._pos];
            if (_pos < _input.Length)
                Advance();

            return DecodeEntities(value);
        }

        var valueStart = _pos;
        while (_pos < _input.Length && Peek() is not ('>' or '/' or ' ' or '\t' or '\r' or '\n'))
            Advance();

        return DecodeEntities(_input[valueStart.._pos]);
    }

    private string ReadName()
    {
        var start = _pos;
        while (_pos < _input.Length && IsNameChar(Peek()))
            Advance();
        return _input[start.._pos];
    }

    private bool TrySkipCommentOrDeclaration()
    {
        if (StartsWith("<!--"))
        {
            var end = _input.IndexOf("-->", _pos, StringComparison.Ordinal);
            _pos = end >= 0 ? end + 3 : _input.Length;
            return true;
        }

        if (StartsWith("<!"))
        {
            SkipUntilTagEnd();
            return true;
        }

        return false;
    }

    private bool SkipUntilTagEnd()
    {
        var quote = '\0';
        while (_pos < _input.Length)
        {
            var c = Peek();
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
                Advance();
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                Advance();
                continue;
            }

            Advance();
            if (c == '>')
                return true;
        }

        return false;
    }

    private bool StartsWith(string value) =>
        string.Compare(_input, _pos, value, 0, value.Length, StringComparison.Ordinal) == 0;

    private static bool IsNameChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '_' or ':' or '-';

    private static bool IsVoidElement(string name) =>
        name is "br" or "img" or "hr" or "input";

    private static string DecodeEntities(string value) =>
        WebUtility.HtmlDecode(value) ?? value;

    private char Peek() => _input[_pos];
    private char? PeekOrNull() => _pos < _input.Length ? _input[_pos] : null;
    private void Advance() => _pos++;

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(Peek()))
            Advance();
    }
}
