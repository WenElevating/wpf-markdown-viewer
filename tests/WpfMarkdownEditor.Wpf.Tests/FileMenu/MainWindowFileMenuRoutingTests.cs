using System.IO;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class MainWindowFileMenuRoutingTests
{
    [Fact]
    public void FileMenu_ContainsApprovedItemsAndOmitsSaveAll()
    {
        var xaml = LoadMainWindowXaml();

        Assert.Contains("x:Name=\"FileMenuItemsPanel\"", xaml);
        Assert.Contains("Click=\"OnNewWindow\"", xaml);
        Assert.Contains("Loc.MainWindow.NewWindow", xaml);
        Assert.Contains("Click=\"OnOpenFolder\"", xaml);
        Assert.Contains("Loc.MainWindow.OpenFolder", xaml);
        Assert.Contains("Click=\"OnQuickOpen\"", xaml);
        Assert.Contains("Loc.MainWindow.QuickOpen", xaml);
        Assert.DoesNotContain("SaveAll", xaml);
        Assert.DoesNotContain("Save All Open Files", xaml);
    }

    [Fact]
    public void OpenRecentFile_UsesNestedMenuInsteadOfQuickOpenDialog()
    {
        var xaml = LoadMainWindowXaml();
        var code = LoadMainWindowCode();

        Assert.Contains("x:Name=\"OpenRecentFileButton\"", xaml);
        Assert.Contains("x:Name=\"RecentFilesPopup\"", xaml);
        Assert.Contains("PlacementTarget=\"{Binding ElementName=OpenRecentFileButton}\"", xaml);
        Assert.Contains("x:Name=\"RecentFilesList\"", xaml);
        Assert.Contains("x:Name=\"RecentFileItemsPanel\"", xaml);
        Assert.Contains("Click=\"OnClearRecentFiles\"", xaml);
        Assert.Contains("Loc.MainWindow.ClearRecentFiles", xaml);
        Assert.Contains("button.Click += OnRecentFileItemClick", code);

        var method = ExtractMethod(code, "OpenRecentFileMenu");
        Assert.DoesNotContain("ShowQuickOpenDialog", method);
        Assert.DoesNotContain("QuickOpenDialog", method);
    }

    [Fact]
    public void FilesSidebar_UsesTreeViewBindingForWorkspaceNodes()
    {
        var xaml = LoadMainWindowXaml();

        Assert.Contains("x:Name=\"TabFiles\"", xaml);
        Assert.Contains("MouseLeftButtonDown=\"OnTabFiles\"", xaml);
        Assert.Contains("x:Name=\"FilesTree\"", xaml);
        Assert.Contains("SelectedItemChanged=\"OnFilesTreeSelectedItemChanged\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Children}\"", xaml);
        Assert.Contains("IsExpanded, Mode=TwoWay", xaml);
        Assert.Contains("IsSelected, Mode=TwoWay", xaml);
    }

    private static string LoadMainWindowXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "samples",
            "WpfMarkdownEditor.Sample",
            "MainWindow.xaml"));
    }

    private static string LoadMainWindowCode()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "samples",
            "WpfMarkdownEditor.Sample",
            "MainWindow.xaml.cs"));
    }

    private static string ExtractMethod(string code, string methodName)
    {
        var start = code.IndexOf($"private void {methodName}", StringComparison.Ordinal);
        Assert.True(start >= 0, methodName);

        var brace = code.IndexOf('{', start);
        Assert.True(brace >= 0, methodName);

        var depth = 0;
        for (var index = brace; index < code.Length; index++)
        {
            if (code[index] == '{')
                depth++;
            else if (code[index] == '}')
            {
                depth--;
                if (depth == 0)
                    return code[start..(index + 1)];
            }
        }

        throw new InvalidOperationException($"Could not parse {methodName}.");
    }
}
