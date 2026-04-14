using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class BlockquoteRenderer(EditorTheme theme) : IBlockRenderer
{
    private readonly FlowDocumentRenderer _innerRenderer = new(theme);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var bq = (BlockquoteBlock)block;
        var section = new Section
        {
            Background = new SolidColorBrush(theme.BlockquoteBackground),
            BorderBrush = new SolidColorBrush(theme.BlockquoteBorder),
            BorderThickness = new Thickness(theme.BlockquoteBorderWidth, 0, 0, 0),
            Padding = new Thickness(theme.BlockquotePaddingLeft, 8, 16, 8),
            Margin = new Thickness(0, 4, 0, 4),
        };

        foreach (var child in bq.Children)
        {
            var rendered = _innerRenderer.RenderBlock(child);
            if (rendered is not null)
                section.Blocks.Add(rendered);
        }

        return section;
    }
}
