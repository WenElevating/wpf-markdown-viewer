namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Base lexer providing common tokenization utilities.
/// Subclasses implement language-specific keyword/comment/string rules.
/// </summary>
public abstract class Lexer
{
    protected abstract HashSet<string> Keywords { get; }
    protected abstract HashSet<string> Types { get; }

    protected virtual bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_';

    protected static bool IsNumberChar(char c) =>
        char.IsDigit(c) || c == '.';

    protected static bool IsWhitespace(char c) =>
        c == ' ' || c == '\t' || c == '\r' || c == '\n';

    protected (string token, int length) ReadWord(string code, int start)
    {
        var i = start;
        while (i < code.Length && IsWordChar(code[i])) i++;
        return (code[start..i], i - start);
    }

    protected (string token, int length) ReadNumber(string code, int start)
    {
        var i = start;
        while (i < code.Length && IsNumberChar(code[i])) i++;
        return (code[start..i], i - start);
    }

    protected (string token, int length) ReadWhitespace(string code, int start)
    {
        var i = start;
        while (i < code.Length && IsWhitespace(code[i])) i++;
        return (code[start..i], i - start);
    }

    protected static (string text, int length) ReadString(string code, int start, char quote, bool breakOnNewline = false)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            if (code[i] == '\\') { i += 2; continue; }
            if (code[i] == quote) { i++; break; }
            if (breakOnNewline && code[i] == '\n') break;
            i++;
        }
        return (code[start..i], i - start);
    }

    protected TokenType ClassifyWord(string word)
    {
        if (Keywords.Contains(word)) return TokenType.Keyword;
        if (Types.Contains(word)) return TokenType.Type;
        return TokenType.Identifier;
    }
}
