using System.Text.RegularExpressions;

namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Shared GFM table parsing utilities used by both the block parser and translation pipeline.
/// </summary>
internal static class TableParserHelpers
{
    private static readonly Regex SeparatorCellRegex = new(@"^:?-+:?$", RegexOptions.Compiled);

    /// <summary>
    /// Returns true when the line is a GFM table separator row (e.g. | --- | :---: | ---: |).
    /// </summary>
    public static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|')) return false;

        var cells = trimmed.Trim('|').Trim().Split('|');
        return cells.All(c =>
        {
            var t = c.Trim();
            return t.Length >= 1 && SeparatorCellRegex.IsMatch(t);
        });
    }
}
