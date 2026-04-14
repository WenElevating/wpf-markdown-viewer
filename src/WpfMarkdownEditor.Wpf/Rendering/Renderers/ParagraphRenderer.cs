using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class ParagraphRenderer(EditorTheme theme) : IBlockRenderer
{
    private readonly InlineRenderer _inlineRenderer = new(theme);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var para = (ParagraphBlock)block;
        var paragraph = new Paragraph
        {
            FontFamily = theme.BodyFont,
            Foreground = new SolidColorBrush(theme.ForegroundColor),
            Margin = new Thickness(0, 0, 0, theme.ParagraphSpacing),
        };

        _inlineRenderer.RenderInlines(paragraph, para.Inlines);
        return paragraph;
    }
}
