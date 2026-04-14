using System.Diagnostics;
using Xunit;
using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

public class ParserPerformanceTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void Parse_SmallDocument_Under16ms()
    {
        var md = GenerateMarkdown(50);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
            _parser.Parse(md);
        sw.Stop();
        var avgMs = sw.ElapsedMilliseconds / 100.0;
        Assert.True(avgMs < 16, $"Average parse time {avgMs:F2}ms exceeds 16ms target");
    }

    [Fact]
    public void Parse_MediumDocument_Under50ms()
    {
        var md = GenerateMarkdown(500);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
            _parser.Parse(md);
        sw.Stop();
        var avgMs = sw.ElapsedMilliseconds / 50.0;
        Assert.True(avgMs < 50, $"Average parse time {avgMs:F2}ms exceeds 50ms target");
    }

    [Fact]
    public void Parse_LargeDocument_Completes()
    {
        var md = GenerateMarkdown(2000);
        var result = _parser.Parse(md);
        Assert.NotEmpty(result);
    }

    private static string GenerateMarkdown(int lines)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < lines; i++)
        {
            var mod = i % 8;
            _ = mod switch
            {
                0 => sb.AppendLine($"# Heading {i}"),
                1 => sb.AppendLine("This is a paragraph with **bold** and *italic* text."),
                2 => sb.AppendLine("- List item with `inline code`"),
                3 => sb.AppendLine($"> Blockquote line {i}"),
                4 => sb.AppendLine("```csharp"),
                5 => sb.AppendLine($"var x = {i};"),
                6 => sb.AppendLine("```"),
                _ => sb.AppendLine($"| Col1 | Col2 |\n| --- | --- |\n| A{i} | B{i} |"),
            };
        }
        return sb.ToString();
    }
}
