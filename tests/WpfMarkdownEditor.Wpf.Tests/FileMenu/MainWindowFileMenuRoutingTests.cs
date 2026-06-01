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
}
