namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

public sealed class BashLexer : Lexer, ISyntaxHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "if", "then", "else", "elif", "fi", "for", "in", "do", "done", "while",
        "until", "case", "esac", "function", "select", "time", "coproc", "return",
        "break", "continue", "export", "readonly", "local", "declare"
    ];

    private static readonly HashSet<string> s_types = [];

    protected override HashSet<string> Keywords => s_keywords;
    protected override HashSet<string> Types => s_types;

    protected override bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '-';

    public bool SupportsLanguage(string language) =>
        language.Equals("bash", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("sh", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("shell", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("zsh", StringComparison.OrdinalIgnoreCase);

    public List<SyntaxToken> Tokenize(string code)
    {
        var tokens = new List<SyntaxToken>();
        var i = 0;

        while (i < code.Length)
        {
            var c = code[i];

            if (c == '#')
            {
                var end = code.IndexOf('\n', i);
                var len = end < 0 ? code.Length - i : end - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            if (c == '"' || c == '\'' || c == '`')
            {
                var (text, len) = ReadString(code, i, c, breakOnNewline: c != '`');
                tokens.Add(new SyntaxToken(TokenType.String, text));
                i += len;
                continue;
            }

            if (char.IsDigit(c))
            {
                var (num, len) = ReadNumber(code, i);
                tokens.Add(new SyntaxToken(TokenType.Number, num));
                i += len;
                continue;
            }

            if (IsWhitespace(c))
            {
                var (ws, len) = ReadWhitespace(code, i);
                tokens.Add(new SyntaxToken(TokenType.Whitespace, ws));
                i += len;
                continue;
            }

            if (c == '$')
            {
                var start = i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_' || code[i] == '{' || code[i] == '}'))
                    i++;
                tokens.Add(new SyntaxToken(TokenType.Identifier, code[start..i]));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var (word, len) = ReadWord(code, i);
                tokens.Add(new SyntaxToken(ClassifyWord(word), word));
                i += len;
                continue;
            }

            tokens.Add(new SyntaxToken(TokenType.Punctuation, c.ToString()));
            i++;
        }

        return tokens;
    }
}
