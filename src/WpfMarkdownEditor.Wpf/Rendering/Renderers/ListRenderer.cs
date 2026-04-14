using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;
using CoreListItem = WpfMarkdownEditor.Core.Parsing.Blocks.ListItem;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class ListRenderer(EditorTheme theme) : IBlockRenderer
{
    private readonly InlineRenderer _inlineRenderer = new(theme);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var list = (ListBlock)block;
        var markerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;

        var listEl = new List
        {
            MarkerStyle = markerStyle,
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(20, 0, 0, 0),
        };

        foreach (var item in list.Items)
        {
            var listItem = new System.Windows.Documents.ListItem();
            foreach (var itemBlock in item.Blocks)
            {
                if (itemBlock is ParagraphBlock para)
                {
                    var p = new Paragraph
                    {
                        FontFamily = theme.BodyFont,
                        Foreground = new SolidColorBrush(theme.ForegroundColor),
                        Margin = new Thickness(0),
                    };
                    _inlineRenderer.RenderInlines(p, para.Inlines);
                    listItem.Blocks.Add(p);
                }
            }
            listEl.ListItems.Add(listItem);
        }

        return listEl;
    }
}
