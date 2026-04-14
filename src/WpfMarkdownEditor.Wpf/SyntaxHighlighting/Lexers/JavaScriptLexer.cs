namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

public sealed class JavaScriptLexer : Lexer, ISyntaxHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "async", "await", "break", "case", "catch", "class", "const",
        "continue", "debugger", "default", "delete", "do", "else", "export",
        "extends", "false", "finally", "for", "function", "if", "import",
        "in", "instanceof", "let", "new", "null", "of", "return", "static",
        "super", "switch", "this", "throw", "true", "try", "typeof",
        "undefined", "var", "void", "while", "with", "yield", "from",
        "as", "implements", "interface", "package", "private", "protected",
        "public", "enum", "type", "declare", "abstract", "readonly"
    ];

    private static readonly HashSet<string> s_types =
    [
        "string", "number", "boolean", "object", "symbol", "bigint",
        "any", "never", "unknown", "void"
    ];

    protected override HashSet<string> Keywords => s_keywords;
    protected override HashSet<string> Types => s_types;

    public bool SupportsLanguage(string language) =>
        language.Equals("javascript", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("js", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("typescript", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("ts", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("jsx", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("tsx", StringComparison.OrdinalIgnoreCase);

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

            // Template literal (backtick string)
            if (c == '`')
            {
                var (str, len) = ReadTemplateLiteral(code, i);
                tokens.Add(new SyntaxToken(TokenType.String, str));
                i += len;
                continue;
            }

            // String literal
            if (c == '"' || c == '\'')
            {
                var (str, len) = ReadString(code, i, c);
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

            // Word
            if (char.IsLetter(c) || c == '_' || c == '$')
            {
                var start = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_' || code[i] == '$')) i++;
                var word = code[start..i];
                tokens.Add(new SyntaxToken(ClassifyWord(word), word));
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

    private static (string text, int length) ReadTemplateLiteral(string code, int start)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            if (code[i] == '\\') { i += 2; continue; }
            if (code[i] == '`') { i++; break; }
            i++;
        }
        return (code[start..i], i - start);
    }
}
