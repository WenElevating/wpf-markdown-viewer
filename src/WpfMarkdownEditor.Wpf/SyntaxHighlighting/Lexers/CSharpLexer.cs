using System.Globalization;

namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

public sealed class CSharpLexer : Lexer, ISyntaxHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default",
        "delegate", "do", "double", "else", "enum", "event", "explicit",
        "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object",
        "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while", "async", "await", "var", "dynamic", "record",
        "init", "with", "required", "nint", "nuint", "global", "managed",
        "unmanaged", "file", "scoped", "allows", "notnull"
    ];

    private static readonly HashSet<string> s_types =
    [
        "bool", "byte", "char", "decimal", "double", "float", "int", "long",
        "object", "sbyte", "short", "string", "uint", "ulong", "ushort",
        "void", "var", "dynamic", "nint", "nuint"
    ];

    protected override HashSet<string> Keywords => s_keywords;
    protected override HashSet<string> Types => s_types;

    public bool SupportsLanguage(string language) =>
        language.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("cs", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("c#", StringComparison.OrdinalIgnoreCase);

    public List<SyntaxToken> Tokenize(string code)
    {
        var tokens = new List<SyntaxToken>();
        var i = 0;

        while (i < code.Length)
        {
            var c = code[i];

            // Single-line comment
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                var end = code.IndexOf('\n', i);
                var len = end < 0 ? code.Length - i : end - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            // Multi-line comment
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var len = end < 0 ? code.Length - i : end + 2 - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            // String literal
            if (c == '"')
            {
                var (str, len) = ReadString(code, i, '"');
                tokens.Add(new SyntaxToken(TokenType.String, str));
                i += len;
                continue;
            }

            // Verbatim string
            if (c == '@' && i + 1 < code.Length && code[i + 1] == '"')
            {
                var end = code.IndexOf("\"\"", i + 2, StringComparison.Ordinal);
                while (end >= 0 && end + 2 < code.Length && code[end + 2] == '"')
                    end = code.IndexOf("\"\"", end + 3, StringComparison.Ordinal);
                var closeIdx = end < 0 ? code.Length : end + 1;
                tokens.Add(new SyntaxToken(TokenType.String, code[i..closeIdx]));
                i = closeIdx;
                continue;
            }

            // Char literal
            if (c == '\'')
            {
                var (str, len) = ReadString(code, i, '\'');
                tokens.Add(new SyntaxToken(TokenType.String, str));
                i += len;
                continue;
            }

            // Number
            if (char.IsDigit(c) || (c == '.' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
            {
                var (num, len) = ReadNumber(code, i);
                tokens.Add(new SyntaxToken(TokenType.Number, num));
                i += len;
                continue;
            }

            // Whitespace
            if (IsWhitespace(c))
            {
                var (ws, len) = ReadWhitespace(code, i);
                tokens.Add(new SyntaxToken(TokenType.Whitespace, ws));
                i += len;
                continue;
            }

            // Word (identifier / keyword)
            if (char.IsLetter(c) || c == '_')
            {
                var (word, len) = ReadWord(code, i);
                tokens.Add(new SyntaxToken(ClassifyWord(word), word));
                i += len;
                continue;
            }

            // Operator / punctuation
            tokens.Add(new SyntaxToken(TokenType.Punctuation, c.ToString()));
            i++;
        }

        return tokens;
    }

    private static (string text, int length) ReadString(string code, int start, char quote)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            if (code[i] == '\\') { i += 2; continue; }
            if (code[i] == quote) { i++; break; }
            i++;
        }
        return (code[start..i], i - start);
    }
}
