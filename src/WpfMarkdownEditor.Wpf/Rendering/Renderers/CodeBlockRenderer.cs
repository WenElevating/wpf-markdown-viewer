using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class CodeBlockRenderer(EditorTheme theme) : IBlockRenderer
{
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

        paragraph.Inlines.Add(new Run(code.Code));
        return paragraph;
    }
}
