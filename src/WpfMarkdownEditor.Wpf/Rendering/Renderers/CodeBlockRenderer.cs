using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.SyntaxHighlighting;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class CodeBlockRenderer(EditorTheme theme, SyntaxHighlighter? highlighter = null) : IBlockRenderer
{
    private readonly SolidColorBrush _defaultBrush = new(theme.CodeForeground);
    private readonly SolidColorBrush _keywordBrush = new(theme.SyntaxKeywordColor);
    private readonly SolidColorBrush _commentBrush = new(theme.SyntaxCommentColor);
    private readonly SolidColorBrush _stringBrush = new(theme.SyntaxStringColor);
    private readonly SolidColorBrush _numberBrush = new(theme.SyntaxNumberColor);
    private readonly SolidColorBrush _typeBrush = new(theme.SyntaxTypeColor);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var code = (CodeBlock)block;
        var paragraph = new Paragraph
        {
            FontFamily = theme.CodeFont,
            FontSize = 13,
            Foreground = new SolidColorBrush(theme.CodeForeground),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0),
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

        // Wrap in Section for background + border (GitHub-style)
        var section = new Section
        {
            Background = new SolidColorBrush(theme.CodeBackground),
            BorderBrush = new SolidColorBrush(theme.CodeBlockBorderColor),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            Margin = new Thickness(0, 8, 0, 8),
        };
        section.Blocks.Add(paragraph);
        return section;
    }

    private Brush GetTokenBrush(TokenType type) => type switch
    {
        TokenType.Keyword => _keywordBrush,
        TokenType.Comment => _commentBrush,
        TokenType.String => _stringBrush,
        TokenType.Number => _numberBrush,
        TokenType.Type => _typeBrush,
        _ => _defaultBrush
    };
}
