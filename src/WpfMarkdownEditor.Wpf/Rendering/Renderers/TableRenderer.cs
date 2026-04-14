using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class TableRenderer(EditorTheme theme) : IBlockRenderer
{
    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var table = (TableBlock)block;
        var tableEl = new Table
        {
            Margin = new Thickness(0, 8, 0, 8),
            BorderBrush = new SolidColorBrush(theme.TableBorderColor),
            BorderThickness = new Thickness(1),
        };

        var columns = Math.Max(table.Headers.Count,
            table.Rows.Count > 0 ? table.Rows.Max(r => r.Count) : 0);

        for (var i = 0; i < columns; i++)
            tableEl.Columns.Add(new TableColumn());

        var rg = new TableRowGroup();

        // Header row
        var headerRow = new TableRow { Background = new SolidColorBrush(theme.TableHeaderBackground) };
        for (var i = 0; i < columns; i++)
        {
            var text = i < table.Headers.Count ? table.Headers[i] : "";
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(text))
            {
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(8, 4, 8, 4),
            }));
        }
        rg.Rows.Add(headerRow);

        // Data rows
        foreach (var row in table.Rows)
        {
            var dataRow = new TableRow();
            for (var i = 0; i < columns; i++)
            {
                var text = i < row.Count ? row[i] : "";
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(text))
                {
                    Padding = new Thickness(8, 4, 8, 4),
                }));
            }
            rg.Rows.Add(dataRow);
        }

        tableEl.RowGroups.Add(rg);
        return tableEl;
    }
}
