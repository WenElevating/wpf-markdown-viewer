using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class HeadingRenderer(EditorTheme theme) : IBlockRenderer
{
    private readonly InlineRenderer _inlineRenderer = new(theme);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var heading = (HeadingBlock)block;
        var paragraph = new Paragraph
        {
            FontFamily = theme.HeadingFont,
            Foreground = new SolidColorBrush(theme.HeadingColor),
            FontSize = GetFontSize(heading.Level),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, theme.HeadingMarginTop, 0, theme.HeadingMarginBottom),
        };

        _inlineRenderer.RenderInlines(paragraph, heading.Inlines);
        return paragraph;
    }

    private static double GetFontSize(int level) => level switch
    {
        1 => 28,
        2 => 24,
        3 => 20,
        4 => 18,
        5 => 16,
        6 => 14,
        _ => 14,
    };
}
