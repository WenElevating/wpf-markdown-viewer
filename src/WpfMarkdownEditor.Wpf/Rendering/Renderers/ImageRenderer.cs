using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class ImageRenderer(EditorTheme theme) : IBlockRenderer
{
    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var image = (ImageBlock)block;
        var container = new BlockUIContainer
        {
            Margin = new Thickness(0, 8, 0, 8),
        };

        var text = new TextBlock
        {
            Text = $"[{image.Alt ?? "Image"}]",
            Foreground = new SolidColorBrush(theme.LinkColor),
            FontStyle = FontStyles.Italic,
        };

        container.Child = text;
        return container;
    }
}
