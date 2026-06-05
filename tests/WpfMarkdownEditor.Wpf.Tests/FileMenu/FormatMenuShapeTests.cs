using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class FormatMenuShapeTests
{
    [Theory]
    [InlineData("OnBold", "Loc.MainWindow.Bold", "Ctrl+B")]
    [InlineData("OnItalic", "Loc.MainWindow.Italic", "Ctrl+I")]
    [InlineData("OnUnderline", "Loc.MainWindow.Underline", "Ctrl+U")]
    [InlineData("OnInlineCode", "Loc.MainWindow.InlineCode", "Ctrl+Shift+`")]
    [InlineData("OnStrikethrough", "Loc.MainWindow.Strikethrough", "Alt+Shift+5")]
    [InlineData("OnComment", "Loc.MainWindow.Comment", "")]
    [InlineData("OnLink", "Loc.MainWindow.Hyperlink", "Ctrl+K")]
    [InlineData("OnClearStyle", "Loc.MainWindow.ClearStyle", "Ctrl+\\")]
    public void FormatMenu_ContainsFirstVersionItems(string handler, string resourceKey, string shortcut)
    {
        var formatMenu = ExtractFormatMenuXaml();

        Assert.Contains($"Click=\"{handler}\"", formatMenu);
        Assert.Contains(resourceKey, formatMenu);
        if (shortcut.Length > 0)
            Assert.Contains($"Tag=\"{shortcut}\"", formatMenu);
    }

    [Theory]
    [InlineData("LinkOperations")]
    [InlineData("ImageOperations")]
    [InlineData("OnLinkOperations")]
    [InlineData("OnImageOperations")]
    public void FormatMenu_OmitsDeferredScreenshotSubmenus(string deferredToken)
    {
        var formatMenu = ExtractFormatMenuXaml();

        Assert.DoesNotContain(deferredToken, formatMenu);
    }

    [Fact]
    public void InsertMenu_StillContainsExistingLinkItem()
    {
        var xaml = LoadMainWindowXaml();

        var insertStart = xaml.IndexOf("x:Name=\"InsertPopup\"", StringComparison.Ordinal);
        Assert.True(insertStart >= 0);
        var viewStart = xaml.IndexOf("x:Name=\"ViewMenuBtn\"", insertStart, StringComparison.Ordinal);
        Assert.True(viewStart > insertStart);
        var insertMenu = xaml[insertStart..viewStart];

        Assert.Contains("Click=\"OnLink\"", insertMenu);
        Assert.Contains("Loc.MainWindow.Link", insertMenu);
    }

    private static string ExtractFormatMenuXaml()
    {
        var xaml = LoadMainWindowXaml();
        var start = xaml.IndexOf("x:Name=\"FormatPopup\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("x:Name=\"InsertMenuBtn\"", start, StringComparison.Ordinal);
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
