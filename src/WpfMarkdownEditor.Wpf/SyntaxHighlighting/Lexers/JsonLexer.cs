namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

public sealed class JsonLexer : Lexer, ISyntaxHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "true", "false", "null"
    ];

    private static readonly HashSet<string> s_types = [];

    protected override HashSet<string> Keywords => s_keywords;
    protected override HashSet<string> Types => s_types;

    public bool SupportsLanguage(string language) =>
        language.Equals("json", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("jsonc", StringComparison.OrdinalIgnoreCase);

    public List<SyntaxToken> Tokenize(string code)
    {
        var tokens = new List<SyntaxToken>();
        var i = 0;

        while (i < code.Length)
        {
            var c = code[i];

            // JSONC single-line comment
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                var end = code.IndexOf('\n', i);
                var len = end < 0 ? code.Length - i : end - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            // JSONC block comment
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var len = end < 0 ? code.Length - i : end + 2 - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            if (c == '"')
            {
                var (text, len) = ReadString(code, i, '"');
                tokens.Add(new SyntaxToken(TokenType.String, text));
                i += len;
                continue;
            }

            if (char.IsDigit(c) || c == '-')
            {
                var start = i;
                i++;
                while (i < code.Length && (char.IsDigit(code[i]) || code[i] is '.' or 'e' or 'E' or '+' or '-'))
                    i++;
                tokens.Add(new SyntaxToken(TokenType.Number, code[start..i]));
                continue;
            }

            if (IsWhitespace(c))
            {
                var (ws, len) = ReadWhitespace(code, i);
                tokens.Add(new SyntaxToken(TokenType.Whitespace, ws));
                i += len;
                continue;
            }

            if (char.IsLetter(c))
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
