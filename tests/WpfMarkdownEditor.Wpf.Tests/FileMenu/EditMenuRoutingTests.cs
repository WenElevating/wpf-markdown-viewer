using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class EditMenuRoutingTests
{
    [Fact]
    public void EditMenu_UsesExplicitCommandTargetsForEditorCommands()
    {
        var xaml = LoadMainWindowXaml();

        Assert.Contains("CommandTarget=\"{Binding ElementName=Editor, Path=TextBox}\"", xaml);
        Assert.Contains("CommandTarget=\"{Binding ElementName=Editor}\"", xaml);
        Assert.Contains("Command=\"{x:Static ctrl:MarkdownEditorCommands.PastePlainText}\"", xaml);
        Assert.Contains("Command=\"{x:Static ctrl:MarkdownEditorCommands.MoveLineUp}\"", xaml);
        Assert.Contains("Command=\"{x:Static ctrl:MarkdownEditorCommands.MoveLineDown}\"", xaml);
        Assert.Contains("Command=\"{x:Static ctrl:MarkdownEditorCommands.DeleteSelectionOrCurrentLine}\"", xaml);
    }

    [Fact]
    public void EditMenu_OmitsDeferredScreenshotItems()
    {
        var xaml = LoadMainWindowXaml();

        Assert.DoesNotContain("SmartPunctuation", xaml);
        Assert.DoesNotContain("SpellCheck", xaml);
        Assert.DoesNotContain("Emoji", xaml);
        Assert.DoesNotContain("CopyImage", xaml);
        Assert.DoesNotContain("CopyHtml", xaml);
        Assert.DoesNotContain("ReplaceAll", xaml);
    }

    [Fact]
    public void PastePlainText_UsesExplicitUnicodeTextFormat()
    {
        var code = LoadMarkdownEditorCode();

        Assert.Contains("Clipboard.GetText(TextDataFormat.UnicodeText)", code);
        Assert.DoesNotContain("Clipboard.GetText()", ExtractMethod(code, "OnPastePlainTextExecuted"));
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

    private static string LoadMarkdownEditorCode()
    {
        var directory = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "src",
            "WpfMarkdownEditor.Wpf",
            "Controls",
            "MarkdownEditor.xaml.cs"));
    }

    private static DirectoryInfo FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        if (directory is not null)
            return directory;

        var sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            directory = new DirectoryInfo(sourceDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
                directory = directory.Parent;

            if (directory is not null)
                return directory;
        }

        directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory;
    }

    private static string ExtractMethod(string code, string methodName)
    {
        var marker = $"private void {methodName}";
        var start = code.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, methodName);

        var brace = code.IndexOf('{', start);
        Assert.True(brace >= 0, methodName);

        var depth = 0;
        for (var i = brace; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            if (code[i] == '}') depth--;
            if (depth == 0)
                return code[start..(i + 1)];
        }

        throw new InvalidOperationException($"Could not extract {methodName}.");
    }
}
