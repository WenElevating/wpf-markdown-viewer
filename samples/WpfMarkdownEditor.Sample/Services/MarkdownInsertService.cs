using System.Text;

namespace WpfMarkdownEditor.Sample.Services;

public static class MarkdownInsertService
{
    public static string GenerateTable(int dataRows, int columns)
    {
        var sb = new StringBuilder();
        sb.Append('\n');
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Range(1, columns).Select(i => $"Column {i}")));
        sb.Append(" |\n");
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Repeat("--------", columns)));
        sb.Append(" |\n");

        var cellIndex = 1;
        for (var row = 0; row < dataRows; row++)
        {
            sb.Append("| ");
            sb.Append(string.Join(" | ", Enumerable.Range(0, columns).Select(_ => $"Cell {cellIndex++}")));
            sb.Append(" |\n");
        }

        return sb.ToString();
    }
}
