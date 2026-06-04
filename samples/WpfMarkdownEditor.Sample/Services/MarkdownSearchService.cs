using WpfMarkdownEditor.Sample.Models;

namespace WpfMarkdownEditor.Sample.Services;

public static class MarkdownSearchService
{
    public static SearchResultSet FindMatches(string content, string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return SearchResultSet.Empty;

        var matches = new List<int>();
        var position = 0;
        while ((position = content.IndexOf(searchText, position, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            matches.Add(position);
            position += searchText.Length;
        }

        return matches.Count == 0
            ? SearchResultSet.Empty
            : new SearchResultSet(matches, 0);
    }

    public static SearchResultSet Move(SearchResultSet resultSet, int direction)
    {
        if (resultSet.Matches.Count == 0)
            return SearchResultSet.Empty;

        var index = (resultSet.CurrentIndex + direction + resultSet.Matches.Count) % resultSet.Matches.Count;
        return resultSet with { CurrentIndex = index };
    }
}
