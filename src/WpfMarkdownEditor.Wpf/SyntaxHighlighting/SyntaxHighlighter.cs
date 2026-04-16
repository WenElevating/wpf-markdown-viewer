using WpfMarkdownEditor.Wpf.SyntaxHighlighting.Lexers;

namespace WpfMarkdownEditor.Wpf.SyntaxHighlighting;

/// <summary>
/// Dispatches code to the appropriate language lexer.
/// Falls back to plain text when no lexer matches.
/// </summary>
public sealed class SyntaxHighlighter : ISyntaxHighlighter
{
    private const int MaxCacheEntries = 128;
    private const int MaxCacheCodeLength = 16_384;

    private readonly SyntaxHighlighterRegistry _registry;
    private readonly Dictionary<string, string> _aliases;
    private readonly Dictionary<(string code, string language), CacheEntry> _tokenCache = [];
    private readonly LinkedList<(string code, string language)> _lru = [];
    private readonly object _cacheLock = new();

    public SyntaxHighlighter()
        : this(CreateDefaultRegistry(), CreateDefaultAliases())
    {
    }

    public SyntaxHighlighter(SyntaxHighlighterRegistry registry, Dictionary<string, string>? aliases = null)
    {
        _registry = registry;
        _aliases = aliases ?? CreateDefaultAliases();
    }

    public bool SupportsLanguage(string language)
    {
        var normalized = NormalizeLanguage(language);
        return normalized is not null && _registry.AnySupports(normalized);
    }

    public List<SyntaxToken> Tokenize(string code)
    {
        // Plain text — single token
        return [new SyntaxToken(TokenType.Plain, code)];
    }

    /// <summary>
    /// Tokenize code for a specific language. Returns plain tokens if language is unsupported.
    /// </summary>
    public List<SyntaxToken> Tokenize(string code, string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (normalized is null)
            return [new SyntaxToken(TokenType.Plain, code)];

        if (TryGetCachedTokens(code, normalized, out var cachedTokens))
            return cachedTokens;

        var lexer = _registry.Find(normalized);
        if (lexer is null)
            return [new SyntaxToken(TokenType.Plain, code)];

        var tokens = lexer.Tokenize(code);
        CacheTokens(code, normalized, tokens);
        return tokens;
    }

    private string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        var key = language.Trim().ToLowerInvariant();
        return _aliases.TryGetValue(key, out var alias) ? alias : key;
    }

    private static SyntaxHighlighterRegistry CreateDefaultRegistry() =>
        new SyntaxHighlighterRegistry()
            .Register(new CSharpLexer())
            .Register(new JavaScriptLexer())
            .Register(new PythonLexer())
            .Register(new JsonLexer())
            .Register(new SqlLexer())
            .Register(new BashLexer());

    private static Dictionary<string, string> CreateDefaultAliases() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = "csharp",
        ["csharp"] = "csharp",
        ["cs"] = "csharp",
        ["javascript"] = "javascript",
        ["js"] = "javascript",
        ["node"] = "javascript",
        ["nodejs"] = "javascript",
        ["typescript"] = "typescript",
        ["ts"] = "typescript",
        ["jsx"] = "jsx",
        ["tsx"] = "tsx",
        ["python"] = "python",
        ["py"] = "python",
        ["py3"] = "python",
        ["json"] = "json",
        ["jsonc"] = "jsonc",
        ["sql"] = "sql",
        ["postgres"] = "postgresql",
        ["postgresql"] = "postgresql",
        ["mysql"] = "mysql",
        ["sqlite"] = "sqlite",
        ["bash"] = "bash",
        ["sh"] = "sh",
        ["shell"] = "shell",
        ["zsh"] = "zsh"
    };

    private bool TryGetCachedTokens(string code, string language, out List<SyntaxToken> tokens)
    {
        lock (_cacheLock)
        {
            if (_tokenCache.TryGetValue((code, language), out var entry))
            {
                _lru.Remove(entry.Node);
                _lru.AddFirst(entry.Node);
                tokens = entry.Tokens;
                return true;
            }

            tokens = null!;
            return false;
        }
    }

    private void CacheTokens(string code, string language, List<SyntaxToken> tokens)
    {
        if (code.Length > MaxCacheCodeLength)
            return;

        lock (_cacheLock)
        {
            var key = (code, language);
            if (_tokenCache.TryGetValue(key, out var existing))
            {
                _tokenCache[key] = new CacheEntry(tokens, existing.Node);
                _lru.Remove(existing.Node);
                _lru.AddFirst(existing.Node);
                return;
            }

            var node = new LinkedListNode<(string code, string language)>(key);
            _lru.AddFirst(node);
            _tokenCache[key] = new CacheEntry(tokens, node);

            while (_tokenCache.Count > MaxCacheEntries && _lru.Count > 0)
            {
                var oldest = _lru.Last!;
                _tokenCache.Remove(oldest.Value);
                _lru.RemoveLast();
            }
        }
    }

    private sealed record CacheEntry(List<SyntaxToken> Tokens, LinkedListNode<(string code, string language)> Node);
}
