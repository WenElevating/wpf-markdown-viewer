namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

public sealed class SqlLexer : Lexer, ISyntaxHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "select", "from", "where", "group", "by", "order", "having", "limit",
        "insert", "into", "values", "update", "set", "delete", "join", "inner",
        "left", "right", "full", "outer", "on", "as", "and", "or", "not", "in",
        "exists", "between", "like", "is", "null", "distinct", "union", "all",
        "case", "when", "then", "else", "end", "create", "table", "view", "index",
        "drop", "alter", "primary", "key", "foreign", "constraint", "references"
    ];

    private static readonly HashSet<string> s_types =
    [
        "int", "integer", "bigint", "smallint", "decimal", "numeric", "float",
        "double", "real", "char", "varchar", "text", "date", "datetime",
        "timestamp", "time", "boolean", "bool"
    ];

    protected override HashSet<string> Keywords => s_keywords;
    protected override HashSet<string> Types => s_types;

    public bool SupportsLanguage(string language) =>
        language.Equals("sql", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("postgresql", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("mysql", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("sqlite", StringComparison.OrdinalIgnoreCase);

    public List<SyntaxToken> Tokenize(string code)
    {
        var tokens = new List<SyntaxToken>();
        var i = 0;

        while (i < code.Length)
        {
            var c = code[i];

            // Single-line comment
            if (c == '-' && i + 1 < code.Length && code[i + 1] == '-')
            {
                var end = code.IndexOf('\n', i);
                var len = end < 0 ? code.Length - i : end - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            // Block comment
            if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var end = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var len = end < 0 ? code.Length - i : end + 2 - i;
                tokens.Add(new SyntaxToken(TokenType.Comment, code[i..(i + len)]));
                i += len;
                continue;
            }

            if (c == '\'')
            {
                var (text, len) = ReadSqlString(code, i);
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

            if (char.IsLetter(c) || c == '_')
            {
                var (word, len) = ReadWord(code, i);
                tokens.Add(new SyntaxToken(ClassifyWord(word.ToLowerInvariant()), word));
                i += len;
                continue;
            }

            tokens.Add(new SyntaxToken(TokenType.Punctuation, c.ToString()));
            i++;
        }

        return tokens;
    }

    private static (string text, int length) ReadSqlString(string code, int start)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            if (code[i] == '\'')
            {
                if (i + 1 < code.Length && code[i + 1] == '\'')
                {
                    i += 2;
                    continue;
                }

                i++;
                break;
            }

            i++;
        }

        return (code[start..i], i - start);
    }
}
