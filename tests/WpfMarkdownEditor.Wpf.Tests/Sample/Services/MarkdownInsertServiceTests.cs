using WpfMarkdownEditor.Sample.Services;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.Services;

public sealed class MarkdownInsertServiceTests
{
    [Fact]
    public void GenerateTable_CreatesHeaderSeparatorAndCells()
    {
        var markdown = MarkdownInsertService.GenerateTable(dataRows: 2, columns: 3);

        Assert.Contains("| Column 1 | Column 2 | Column 3 |", markdown);
        Assert.Contains("| -------- | -------- | -------- |", markdown);
        Assert.Contains("| Cell 1 | Cell 2 | Cell 3 |", markdown);
        Assert.Contains("| Cell 4 | Cell 5 | Cell 6 |", markdown);
    }
}
