using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class ParagraphMenuShapeTests
{
    [Theory]
    [InlineData("OnHeading1", "Loc.MainWindow.Heading1", "Ctrl+1")]
    [InlineData("OnHeading2", "Loc.MainWindow.Heading2", "Ctrl+2")]
    [InlineData("OnHeading3", "Loc.MainWindow.Heading3", "Ctrl+3")]
    [InlineData("OnHeading4", "Loc.MainWindow.Heading4", "Ctrl+4")]
    [InlineData("OnHeading5", "Loc.MainWindow.Heading5", "Ctrl+5")]
    [InlineData("OnHeading6", "Loc.MainWindow.Heading6", "Ctrl+6")]
    [InlineData("OnParagraphStyle", "Loc.MainWindow.ParagraphStyle", "Ctrl+0")]
    [InlineData("OnQuote", "Loc.MainWindow.Blockquote", "Ctrl+Shift+Q")]
    [InlineData("OnOrderedList", "Loc.MainWindow.OrderedList", "Ctrl+Shift+[")]
    [InlineData("OnUnorderedList", "Loc.MainWindow.BulletList", "Ctrl+Shift+]")]
    [InlineData("OnCodeBlock", "Loc.MainWindow.CodeBlock", "Ctrl+Shift+K")]
    [InlineData("OnTable", "Loc.MainWindow.Table", "")]
    [InlineData("OnHorizontalRule", "Loc.MainWindow.HorizontalRule", "")]
    [InlineData("OnInsertParagraphAbove", "Loc.MainWindow.InsertParagraphAbove", "")]
    [InlineData("OnInsertParagraphBelow", "Loc.MainWindow.InsertParagraphBelow", "")]
    public void ParagraphMenu_ContainsFirstVersionItems(string handler, string resourceKey, string shortcut)
    {
        var paragraphMenu = ExtractParagraphMenuXaml();

        Assert.Contains($"Click=\"{handler}\"", paragraphMenu);
        Assert.Contains(resourceKey, paragraphMenu);
        if (shortcut.Length > 0)
            Assert.Contains($"Tag=\"{shortcut}\"", paragraphMenu);
    }

    [Theory]
    [InlineData("RaiseHeading")]
    [InlineData("LowerHeading")]
    [InlineData("TaskList")]
    [InlineData("TaskStatus")]
    [InlineData("ListIndent")]
    [InlineData("AlertBox")]
    [InlineData("EquationBlock")]
    [InlineData("LinkReference")]
    [InlineData("Footnote")]
    [InlineData("TableOfContents")]
    [InlineData("YamlFrontMatter")]
    public void ParagraphMenu_OmitsDeferredScreenshotItems(string deferredToken)
    {
        var paragraphMenu = ExtractParagraphMenuXaml();

        Assert.DoesNotContain(deferredToken, paragraphMenu);
    }

    [Fact]
    public void InsertMenu_StillContainsExistingBlockInsertionItems()
    {
        var xaml = LoadMainWindowXaml();

        var insertStart = xaml.IndexOf("x:Name=\"InsertPopup\"", StringComparison.Ordinal);
        Assert.True(insertStart >= 0);
        var viewStart = xaml.IndexOf("x:Name=\"ViewMenuBtn\"", insertStart, StringComparison.Ordinal);
        Assert.True(viewStart > insertStart);
        var insertMenu = xaml[insertStart..viewStart];

        Assert.Contains("Click=\"OnCodeBlock\"", insertMenu);
        Assert.Contains("Click=\"OnTable\"", insertMenu);
        Assert.Contains("Click=\"OnHorizontalRule\"", insertMenu);
    }

    private static string ExtractParagraphMenuXaml()
    {
        var xaml = LoadMainWindowXaml();
        var start = xaml.IndexOf("x:Name=\"ParagraphPopup\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("x:Name=\"FormatMenuBtn\"", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return xaml[start..end];
    }

    private static string LoadMainWindowXaml()
    {
        var directory = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "samples",
            "WpfMarkdownEditor.Sample",
            "MainWindow.xaml"));
    }

    private static DirectoryInfo FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        if (directory is not null)
            return directory;

        directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory;
    }
}
