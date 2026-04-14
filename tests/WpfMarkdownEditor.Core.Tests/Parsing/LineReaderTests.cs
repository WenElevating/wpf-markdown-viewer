using Xunit;
using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public class LineReaderTests
{
    [Fact]
    public void ReadLine_SingleLine()
    {
        var reader = new LineReader("Hello");
        var line = reader.ReadLine();
        Assert.NotNull(line);
        Assert.Equal("Hello", line.Content);
        Assert.Equal(1, line.LineNumber);
    }

    [Fact]
    public void ReadLine_TwoLines_LF()
    {
        var reader = new LineReader("Line1\nLine2");
        var first = reader.ReadLine();
        var second = reader.ReadLine();

        Assert.Equal("Line1", first!.Content);
        Assert.Equal(1, first.LineNumber);
        Assert.Equal("Line2", second!.Content);
        Assert.Equal(2, second.LineNumber);
    }

    [Fact]
    public void ReadLine_TwoLines_CRLF()
    {
        var reader = new LineReader("Line1\r\nLine2");
        var first = reader.ReadLine();
        var second = reader.ReadLine();

        Assert.Equal("Line1", first!.Content);
        Assert.Equal("Line2", second!.Content);
    }

    [Fact]
    public void ReadLine_TwoLines_CR()
    {
        var reader = new LineReader("Line1\rLine2");
        var first = reader.ReadLine();
        var second = reader.ReadLine();

        Assert.Equal("Line1", first!.Content);
        Assert.Equal("Line2", second!.Content);
    }

    [Fact]
    public void ReadLine_EmptyString_ReturnsNull()
    {
        var reader = new LineReader("");
        Assert.Null(reader.ReadLine());
    }

    [Fact]
    public void ReadLine_NullInput_ReturnsNull()
    {
        var reader = new LineReader(null!);
        Assert.Null(reader.ReadLine());
    }

    [Fact]
    public void ReadLine_AtEnd_ReturnsNull()
    {
        var reader = new LineReader("Only");
        reader.ReadLine();
        Assert.Null(reader.ReadLine());
    }

    [Fact]
    public void HasMore_TrueWhenData()
    {
        var reader = new LineReader("text");
        Assert.True(reader.HasMore);
    }

    [Fact]
    public void HasMore_FalseWhenEmpty()
    {
        var reader = new LineReader("");
        Assert.False(reader.HasMore);
    }

    [Fact]
    public void HasMore_FalseAfterReading()
    {
        var reader = new LineReader("text");
        reader.ReadLine();
        Assert.False(reader.HasMore);
    }

    [Fact]
    public void PeekLine_DoesNotAdvance()
    {
        var reader = new LineReader("Line1\nLine2");
        var peeked = reader.PeekLine();
        Assert.Equal("Line1", peeked!.Content);

        var read = reader.ReadLine();
        Assert.Equal("Line1", read!.Content);
    }

    [Fact]
    public void PeekLine_AtEnd_ReturnsNull()
    {
        var reader = new LineReader("text");
        reader.ReadLine();
        Assert.Null(reader.PeekLine());
    }

    [Fact]
    public void ReadLine_BlankLine()
    {
        var reader = new LineReader("Line1\n\nLine3");
        reader.ReadLine(); // Line1
        var blank = reader.ReadLine();
        Assert.Equal("", blank!.Content);
    }

    [Fact]
    public void ReadLine_TrailingNewline()
    {
        var reader = new LineReader("Line\n");
        var line = reader.ReadLine();
        Assert.Equal("Line", line!.Content);
        Assert.Null(reader.ReadLine());
    }

    [Fact]
    public void LineInfo_IndentLevel()
    {
        var reader = new LineReader("    indented");
        var line = reader.ReadLine();
        Assert.Equal(3, line!.IndentLevel); // Capped at 3
    }

    [Fact]
    public void LineInfo_StrippedContent()
    {
        var reader = new LineReader("   content");
        var line = reader.ReadLine();
        Assert.Equal("content", line!.StrippedContent());
    }

    [Fact]
    public void LineInfo_NoIndent()
    {
        var reader = new LineReader("no indent");
        var line = reader.ReadLine();
        Assert.Equal(0, line!.IndentLevel);
        Assert.Equal("no indent", line.StrippedContent());
    }

    [Fact]
    public void ReadLine_MultiplePeeks_SameResult()
    {
        var reader = new LineReader("Line1\nLine2");
        var peek1 = reader.PeekLine();
        var peek2 = reader.PeekLine();
        Assert.Equal(peek1!.Content, peek2!.Content);
    }

    [Fact]
    public void ReadLine_LineNumberTracking()
    {
        var reader = new LineReader("A\nB\nC");
        Assert.Equal(1, reader.ReadLine()!.LineNumber);
        Assert.Equal(2, reader.ReadLine()!.LineNumber);
        Assert.Equal(3, reader.ReadLine()!.LineNumber);
    }
}
