using WpfMarkdownEditor.Core.Translation;
using Xunit;

namespace WpfMarkdownEditor.Core.Tests.Translation;

public class MarkdownSegmentExtractorTests
{
    [Fact]
    public void Extract_NoSpecialContent_ReturnsOriginalText()
    {
        var markdown = "# Hello\n\nThis is a paragraph.";
        var (plainText, template, _) = MarkdownSegmentExtractor.Extract(markdown);

        // Should have: text("# "), blank, text("")
        Assert.Equal(3, template.Count);
        Assert.Equal("text", template[0].Type);
        Assert.Equal("# ", template[0].Prefix);
        Assert.Equal("blank", template[1].Type);
        Assert.Equal("text", template[2].Type);
    }

    [Fact]
    public void Extract_FencedCodeBlock_PreservesInTemplate()
    {
        var markdown = "# Title\n\n```csharp\nvar x = 1;\n```\n\nParagraph after.";
        var (plainText, template, _) = MarkdownSegmentExtractor.Extract(markdown);

        // plainText should NOT contain code
        Assert.DoesNotContain("var x = 1;", plainText);
        Assert.Contains("Title", plainText);

        // Template should have a preserve segment with the code block
        var codeSeg = template.Find(s => s.Type == "preserve");
        Assert.NotNull(codeSeg);
        Assert.Contains("```csharp", codeSeg.Raw);
        Assert.Contains("var x = 1;", codeSeg.Raw);
    }

    [Fact]
    public void Extract_MultipleCodeBlocks_AllPreserved()
    {
        var markdown = "```\ncode1\n```\n\ntext\n\n```\ncode2\n```";
        var (plainText, template, _) = MarkdownSegmentExtractor.Extract(markdown);

        var codeSegments = template.FindAll(s => s.Type == "preserve");
        Assert.Equal(2, codeSegments.Count);
        Assert.DoesNotContain("code1", plainText);
        Assert.DoesNotContain("code2", plainText);
    }

    [Fact]
    public void Extract_InlineCode_Tokenized()
    {
        var markdown = "Use the `print()` function.";
        var (plainText, _, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        // Inline code markers should be replaced with tokens
        Assert.DoesNotContain("`print()`", plainText);
        Assert.Contains("print()", plainText); // text content preserved
        Assert.True(inlineTokens.Count >= 2); // start + end tokens
    }

    [Fact]
    public void Extract_BoldItalic_Tokenized()
    {
        var markdown = "This is **bold** and *italic* text.";
        var (plainText, _, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        Assert.DoesNotContain("**", plainText);
        Assert.DoesNotContain("*italic*", plainText);
        Assert.Contains("bold", plainText);
        Assert.Contains("italic", plainText);
    }

    [Fact]
    public void Extract_Link_PreservesUrl()
    {
        var markdown = "Click [here](https://example.com) for info.";
        var (plainText, _, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        Assert.DoesNotContain("https://example.com", plainText);
        Assert.Contains("here", plainText);
        // URL should be stored in a token value
        Assert.Contains(inlineTokens.Values, v => v.Contains("https://example.com"));
    }

    [Fact]
    public void Extract_Table_TranslatableCellsExtracted()
    {
        var markdown = "| Feature | Status |\n| ------- | ------ |\n| Parser | Done |";
        var (plainText, template, _) = MarkdownSegmentExtractor.Extract(markdown);

        // Separator row should be preserved
        var preserveSegs = template.FindAll(s => s.Type == "preserve");
        Assert.Contains(preserveSegs, s => s.Raw != null && s.Raw.Contains("---"));

        // Cell text should be in plainText
        Assert.Contains("Feature", plainText);
        Assert.Contains("Status", plainText);
        Assert.Contains("Parser", plainText);
        Assert.Contains("Done", plainText);
    }

    [Fact]
    public void Extract_BlankLinesAndHR_Preserved()
    {
        var markdown = "# Title\n\n---\n\nParagraph";
        var (plainText, template, _) = MarkdownSegmentExtractor.Extract(markdown);

        var blankSegs = template.FindAll(s => s.Type == "blank");
        Assert.Equal(2, blankSegs.Count);

        var preserveSegs = template.FindAll(s => s.Type == "preserve");
        Assert.Single(preserveSegs);
        Assert.Equal("---", preserveSegs[0].Raw);
    }

    [Fact]
    public void Reconstruct_RoundTrip_NoCode()
    {
        var markdown = "# Title\n\nParagraph with **bold** text.";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        var restored = MarkdownSegmentExtractor.Reconstruct(template, plainText, inlineTokens);
        Assert.Equal(markdown, restored);
    }

    [Fact]
    public void Reconstruct_RoundTrip_WithCodeBlock()
    {
        var markdown = "# Title\n\n```python\nprint('hello')\n```\n\nDone.";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        var restored = MarkdownSegmentExtractor.Reconstruct(template, plainText, inlineTokens);
        Assert.Equal(markdown, restored);
    }

    [Fact]
    public void Reconstruct_TranslatesAndPreservesCode()
    {
        var markdown = "Hello world\n\n```js\nlet x = 1;\n```\n\nGoodbye.";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        // Simulate translation
        var translated = plainText
            .Replace("Hello world", "你好世界")
            .Replace("Goodbye.", "再见。");

        var restored = MarkdownSegmentExtractor.Reconstruct(template, translated, inlineTokens);

        Assert.Contains("你好世界", restored);
        Assert.Contains("再见。", restored);
        Assert.Contains("```js\nlet x = 1;\n```", restored);
    }

    [Fact]
    public void Reconstruct_PreservesHeadingAndListStructure()
    {
        var markdown = "# Title\n\n- Item one\n- Item two\n\n> Quote text";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        // Simulate translation
        var translated = plainText
            .Replace("Title", "标题")
            .Replace("Item one", "项目一")
            .Replace("Item two", "项目二")
            .Replace("Quote text", "引用文本");

        var restored = MarkdownSegmentExtractor.Reconstruct(template, translated, inlineTokens);

        Assert.Contains("# 标题", restored);
        Assert.Contains("- 项目一", restored);
        Assert.Contains("- 项目二", restored);
        Assert.Contains("> 引用文本", restored);
    }

    [Fact]
    public void Reconstruct_PreservesTableStructure()
    {
        var markdown = "| Name | Value |\n| --- | ----- |\n| A | B |";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        var translated = plainText
            .Replace("Name", "名称")
            .Replace("Value", "值")
            .Replace("A", "甲")
            .Replace("B", "乙");

        var restored = MarkdownSegmentExtractor.Reconstruct(template, translated, inlineTokens);

        Assert.Contains("| 名称 | 值 |", restored);
        Assert.Contains("| --- | ----- |", restored);
        Assert.Contains("| 甲 | 乙 |", restored);
    }

    [Fact]
    public void Reconstruct_PreservesInlineCode()
    {
        var markdown = "Use `Console.WriteLine` to print.";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        var translated = plainText.Replace("Use", "使用");
        var restored = MarkdownSegmentExtractor.Reconstruct(template, translated, inlineTokens);

        Assert.Contains("`Console.WriteLine`", restored);
        Assert.Contains("使用", restored);
    }

    [Fact]
    public void Reconstruct_PreservesBoldAndItalic()
    {
        var markdown = "This is **bold** and *italic*.";
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        var translated = plainText
            .Replace("This is", "这是")
            .Replace("and", "和")
            .Replace(".", "。");

        var restored = MarkdownSegmentExtractor.Reconstruct(template, translated, inlineTokens);

        Assert.Contains("**bold**", restored);
        Assert.Contains("*italic*", restored);
    }

    [Fact]
    public void Extract_EmptyInput_ReturnsEmpty()
    {
        var (plainText, template, _) = MarkdownSegmentExtractor.Extract("");

        Assert.Equal("", plainText);
        Assert.Empty(template);
    }
}
