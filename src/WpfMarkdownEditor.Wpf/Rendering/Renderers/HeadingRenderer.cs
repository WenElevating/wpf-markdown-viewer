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

        // GitHub-style: add bottom border for h1 and h2
        if (theme.ShowHeadingBorders && heading.Level <= 2)
        {
            var section = new Section
            {
                BorderBrush = new SolidColorBrush(theme.HeadingBorderColor),
                BorderThickness = new Thickness(0, 0, 0, heading.Level == 1 ? 1 : 1),
                Padding = new Thickness(0, 0, 0, 8),
            };
            section.Blocks.Add(paragraph);
            return section;
        }

        return paragraph;
    }

    private static double GetFontSize(int level) => level switch
    {
        1 => 32,
        2 => 24,
        3 => 20,
        4 => 16,
        5 => 14,
        6 => 13,
        _ => 14,
    };
}
