namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

public sealed class PythonLexer : Lexer, ISyntaxHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "False", "None", "True", "and", "as", "assert", "async", "await",
        "break", "class", "continue", "def", "del", "elif", "else", "except",
        "finally", "for", "from", "global", "if", "import", "in", "is",
        "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try",
        "while", "with", "yield", "match", "case"
    ];

    private static readonly HashSet<string> s_types =
    [
        "int", "float", "str", "bool", "list", "dict", "tuple", "set",
        "bytes", "bytearray", "complex", "range", "type", "object",
        "frozenset", "memoryview"
    ];

    protected override HashSet<string> Keywords => s_keywords;
    protected override HashSet<string> Types => s_types;

    public bool SupportsLanguage(string language) =>
        language.Equals("python", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("py", StringComparison.OrdinalIgnoreCase);

    public List<SyntaxToken> Tokenize(string code)
    {
        var tokens = new List<SyntaxToken>();
        var i = 0;

        while (i < code.Length)
        {
            var c = code[i];

            // Single-line comment (# ...)
            if (c == '#')
            {
                var end = code.IndexOf('\n', i);
                var len = end < 0 ? code.Length - i : end - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            // Triple-quoted string (""" or ''')
            if ((c == '"' || c == '\'') && i + 2 < code.Length &&
                code[i + 1] == c && code[i + 2] == c)
            {
                var (str, len) = ReadTripleQuoted(code, i, c);
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

            // Decorator (@)
            if (c == '@')
            {
                tokens.Add(new SyntaxToken(TokenType.Punctuation, "@"));
                i++;
                // Read decorator name
                if (i < code.Length && (char.IsLetter(code[i]) || code[i] == '_'))
                {
                    var (word, wlen) = ReadWord(code, i);
                    tokens.Add(new SyntaxToken(TokenType.Identifier, word));
                    i += wlen;
                }
                continue;
            }

            // Number
            if (char.IsDigit(c) || (c == '.' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
            {
                // Hex/octal/binary prefixes
                if (c == '0' && i + 1 < code.Length)
                {
                    var next = code[i + 1];
                    if (next == 'x' || next == 'X' || next == 'o' || next == 'O' ||
                        next == 'b' || next == 'B')
                    {
                        var start = i;
                        i += 2;
                        while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
                        tokens.Add(new SyntaxToken(TokenType.Number, code[start..i]));
                        continue;
                    }
                }
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
            if (code[i] == '\n') break; // Python: unterminated string ends at newline
            i++;
        }
        return (code[start..i], i - start);
    }

    private static (string text, int length) ReadTripleQuoted(string code, int start, char quote)
    {
        var i = start + 3;
        var closeSeq = new string(quote, 3);
        while (i < code.Length)
        {
            if (code[i] == '\\') { i++; continue; }
            if (i + 2 < code.Length && code[i] == quote && code[i + 1] == quote && code[i + 2] == quote)
            {
                i += 3;
                break;
            }
            i++;
        }
        return (code[start..i], i - start);
    }
}
