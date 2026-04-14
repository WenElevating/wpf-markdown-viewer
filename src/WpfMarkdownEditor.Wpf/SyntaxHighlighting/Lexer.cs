namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Base lexer providing common tokenization utilities.
/// Subclasses implement language-specific keyword/comment/string rules.
/// </summary>
public abstract class Lexer
{
    protected abstract HashSet<string> Keywords { get; }
    protected abstract HashSet<string> Types { get; }

    protected static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_';

    protected static bool IsNumberChar(char c) =>
        char.IsDigit(c) || c == '.' || c == 'x' || c == 'X' ||
        (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

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
        // Consume trailing type suffixes (f, d, m, l, u, UL, etc.)
        while (i < code.Length && (code[i] == 'f' || code[i] == 'F' ||
               code[i] == 'd' || code[i] == 'D' || code[i] == 'm' || code[i] == 'M' ||
               code[i] == 'l' || code[i] == 'L' || code[i] == 'u' || code[i] == 'U'))
            i++;
        return (code[start..i], i - start);
    }

    protected (string token, int length) ReadWhitespace(string code, int start)
    {
        var i = start;
        while (i < code.Length && IsWhitespace(code[i])) i++;
        return (code[start..i], i - start);
    }

    protected TokenType ClassifyWord(string word)
    {
        if (Keywords.Contains(word)) return TokenType.Keyword;
        if (Types.Contains(word)) return TokenType.Type;
        return TokenType.Identifier;
    }
}
