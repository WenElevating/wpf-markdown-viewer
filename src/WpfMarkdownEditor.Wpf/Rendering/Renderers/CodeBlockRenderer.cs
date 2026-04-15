using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.SyntaxHighlighting;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class CodeBlockRenderer(EditorTheme theme, SyntaxHighlighter? highlighter = null) : IBlockRenderer
{
    private static readonly SolidColorBrush KeywordBrush = new(Color.FromRgb(0x56, 0x9c, 0xd6));
    private static readonly SolidColorBrush CommentBrush = new(Color.FromRgb(0x6a, 0x99, 0x55));
    private static readonly SolidColorBrush StringBrush = new(Color.FromRgb(0xce, 0x91, 0x78));
    private static readonly SolidColorBrush NumberBrush = new(Color.FromRgb(0xb5, 0xce, 0xa8));
    private static readonly SolidColorBrush TypeBrush = new(Color.FromRgb(0x4e, 0xc9, 0xb0));
    private readonly SolidColorBrush _defaultBrush = new(theme.CodeForeground);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var code = (CodeBlock)block;
        var paragraph = new Paragraph
        {
            FontFamily = theme.CodeFont,
            FontSize = 13,
            Background = new SolidColorBrush(theme.CodeBackground),
            Foreground = new SolidColorBrush(theme.CodeForeground),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 8, 0, 8),
        };

        // Apply syntax highlighting when language hint is available
        if (highlighter is not null && !string.IsNullOrEmpty(code.Language))
        {
            var tokens = highlighter.Tokenize(code.Code, code.Language);
            foreach (var token in tokens)
            {
                var brush = GetTokenBrush(token.Type);
                paragraph.Inlines.Add(new Run(token.Text)
                {
                    Foreground = brush,
                });
            }
        }
        else
        {
            paragraph.Inlines.Add(new Run(code.Code));
        }

        return paragraph;
    }

    private Brush GetTokenBrush(TokenType type) => type switch
    {
        TokenType.Keyword => KeywordBrush,
        TokenType.Comment => CommentBrush,
        TokenType.String => StringBrush,
        TokenType.Number => NumberBrush,
        TokenType.Type => TypeBrush,
        _ => _defaultBrush
    };
}
