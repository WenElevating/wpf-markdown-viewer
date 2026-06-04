using WpfMarkdownEditor.Sample.Services;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.Services;

public sealed class MarkdownSearchServiceTests
{
    [Fact]
    public void FindMatches_ReturnsCaseInsensitivePositions()
    {
        var result = MarkdownSearchService.FindMatches("Alpha beta alpha", "ALPHA");

        Assert.Equal([0, 11], result.Matches);
        Assert.Equal(0, result.CurrentIndex);
    }

    [Fact]
    public void Move_NavigatesCircularly()
    {
        var result = MarkdownSearchService.FindMatches("one one one", "one");

        result = MarkdownSearchService.Move(result, 1);
        Assert.Equal(1, result.CurrentIndex);

        result = MarkdownSearchService.Move(result, -1);
        Assert.Equal(0, result.CurrentIndex);
    }
}
