namespace WpfMarkdownEditor.Sample.Models;

public sealed record SearchResultSet(IReadOnlyList<int> Matches, int CurrentIndex)
{
    public static SearchResultSet Empty { get; } = new([], -1);
}
