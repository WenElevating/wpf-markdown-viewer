using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
        Assert.DoesNotContain("MouseEnter=\"OnOpenRecentFile\"", xaml);
        Assert.Contains("MouseEnter=\"OnOpenRecentFileMouseEnter\"", xaml);
        Assert.Contains("MouseLeave=\"OnOpenRecentFileMouseLeave\"", xaml);
        Assert.Contains("x:Name=\"RecentFilesPopup\"", xaml);
        Assert.Contains("PlacementTarget=\"{Binding ElementName=OpenRecentFileButton}\"", xaml);
        Assert.Contains("PopupAnimation=\"None\"", xaml);
        Assert.Contains("StaysOpen=\"True\"", xaml);
        Assert.Contains("x:Name=\"RecentFilesList\"", xaml);
        Assert.Contains("MouseEnter=\"OnRecentFilesMenuMouseEnter\"", xaml);
        Assert.Contains("MouseLeave=\"OnRecentFilesMenuMouseLeave\"", xaml);
        Assert.Contains("x:Name=\"RecentFileItemsPanel\"", xaml);
        Assert.Contains("Click=\"OnClearRecentFiles\"", xaml);
        Assert.Contains("Loc.MainWindow.ClearRecentFiles", xaml);
        Assert.Contains("button.Click += OnRecentFileItemClick", code);

        var method = ExtractMethod(code, "OpenRecentFileMenu");
        Assert.DoesNotContain("ShowQuickOpenDialog", method);
        Assert.DoesNotContain("QuickOpenDialog", method);
    }

    [Fact]
    public void OpenRecentFile_RendersRecentEntriesFromFastSnapshot()
    {
        var code = LoadMainWindowCode();

        var menuMethod = ExtractMethod(code, "OpenRecentFileMenu");
        Assert.Contains("_recentFilesCacheLoaded", menuMethod);
        Assert.Contains("ShowRecentFilesLoadingState()", menuMethod);
        Assert.Contains("RenderRecentFilesMenu(_recentFilesCache)", menuMethod);
        Assert.Contains("RefreshRecentFilesCacheAsync", menuMethod);
        Assert.DoesNotContain("LoadFiles(", menuMethod);
        Assert.DoesNotContain("await", menuMethod);

        var refreshMethod = ExtractMethod(code, "RefreshRecentFilesCacheAsync");
        Assert.Contains("await _recentFilesService.LoadFilesSnapshotAsync", refreshMethod);
        Assert.DoesNotContain("removeMissingFiles: true", refreshMethod);
    }

    [Fact]
    public void OpenRecentFile_HoverUsesDelayedRouteOnly()
    {
        var code = LoadMainWindowCode();

        var mouseEnterMethod = ExtractMethod(code, "OnOpenRecentFileMouseEnter");
        Assert.Contains("OpenRecentFileMenuAfterHoverDelayAsync", mouseEnterMethod);
        Assert.DoesNotContain("RecentFilesPopup.IsOpen = true", mouseEnterMethod);
        Assert.DoesNotContain("LoadFiles(", mouseEnterMethod);

        var delayedMethod = ExtractMethod(code, "OpenRecentFileMenuAfterHoverDelayAsync");
        Assert.Contains("Task.Delay(TimeSpan.FromMilliseconds(400)", delayedMethod);
        Assert.Contains("OpenRecentFileMenu();", delayedMethod);
        Assert.Contains("OpenRecentFileButton.IsMouseOver", delayedMethod);

        var mouseLeaveMethod = ExtractMethod(code, "OnOpenRecentFileMouseLeave");
        Assert.Contains("CancelRecentFilesHover", mouseLeaveMethod);
        Assert.Contains("ScheduleRecentFilesMenuClose", mouseLeaveMethod);
    }

    [Fact]
    public void OpenRecentFile_ClosesSubmenuWhenPointerLeavesParentAndSubmenu()
    {
        var code = LoadMainWindowCode();

        var submenuEnterMethod = ExtractMethod(code, "OnRecentFilesMenuMouseEnter");
        Assert.Contains("CancelRecentFilesMenuClose", submenuEnterMethod);

        var submenuLeaveMethod = ExtractMethod(code, "OnRecentFilesMenuMouseLeave");
        Assert.Contains("ScheduleRecentFilesMenuClose", submenuLeaveMethod);

        var closeMethod = ExtractMethod(code, "CloseRecentFilesMenuIfPointerLeftAsync");
        Assert.Contains("Task.Delay(TimeSpan.FromMilliseconds(120)", closeMethod);
        Assert.Contains("OpenRecentFileButton.IsMouseOver", closeMethod);
        Assert.Contains("RecentFilesList.IsMouseOver", closeMethod);
        Assert.Contains("RecentFilesPopup.IsOpen = false", closeMethod);
    }

    [Fact]
    public void OpenRecentFile_KeepsParentItemActiveWhileSubmenuIsOpen()
    {
        var xaml = LoadMainWindowXaml();
        var code = LoadMainWindowCode();

        Assert.Contains("Closed=\"OnRecentFilesPopupClosed\"", xaml);
        Assert.Contains("Closed=\"OnFilePopupClosed\"", xaml);

        var menuMethod = ExtractMethod(code, "OpenRecentFileMenu");
        Assert.Contains("SetOpenRecentFileButtonActive(isActive: true)", menuMethod);

        var closedMethod = ExtractMethod(code, "OnRecentFilesPopupClosed");
        Assert.Contains("SetOpenRecentFileButtonActive(isActive: false)", closedMethod);

        var activeMethod = ExtractMethod(code, "SetOpenRecentFileButtonActive");
        Assert.Contains("HoverBackgroundBrush", activeMethod);
        Assert.Contains("ClearValue(BackgroundProperty)", activeMethod);

        var fileClosedMethod = ExtractMethod(code, "OnFilePopupClosed");
        Assert.Contains("RecentFilesPopup.IsOpen = false", fileClosedMethod);
    }

    [Fact]
    public void FilesSidebar_UsesTreeViewBindingForWorkspaceNodes()
    {
        var xaml = LoadMainWindowXaml();

        Assert.Contains("x:Name=\"TabFiles\"", xaml);
        Assert.Contains("MouseLeftButtonDown=\"OnTabFiles\"", xaml);
        Assert.DoesNotContain("x:Name=\"TabHistory\"", xaml);
        Assert.DoesNotContain("x:Name=\"HistoryPanel\"", xaml);
        Assert.Contains("x:Name=\"FilesTree\"", xaml);
        Assert.Contains("SelectedItemChanged=\"OnFilesTreeSelectedItemChanged\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Children}\"", xaml);
        Assert.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\"", xaml);
        Assert.Contains("VirtualizingStackPanel.VirtualizationMode=\"Recycling\"", xaml);
        Assert.Contains("ScrollViewer.CanContentScroll=\"True\"", xaml);
        Assert.Contains("IsExpanded, Mode=TwoWay", xaml);
        Assert.Contains("IsSelected, Mode=TwoWay", xaml);
        Assert.Contains("x:Name=\"FilesEmptyPanel\"", xaml);
        Assert.Contains("Click=\"OnOpenFolder\"", xaml);
        Assert.Contains("Click=\"OnOpenFile\"", xaml);
        Assert.Contains("Loc.MainWindow.NoFolderOpened", xaml);
        Assert.Contains("Loc.MainWindow.OpenFolderHint", xaml);
        Assert.Contains("x:Name=\"FilesEmptyTitle\"", xaml);
        Assert.Contains("x:Name=\"FilesEmptyHint\"", xaml);
        Assert.Contains("x:Key=\"WorkspaceTreeItemStyle\"", xaml);
        Assert.Contains("Text=\"&#xE8B7;\"", xaml);
        Assert.Contains("Text=\"&#xE8A5;\"", xaml);

        var panelStateMethod = ExtractMethod(code: LoadMainWindowCode(), "UpdateFilesPanelState");
        Assert.Contains("Children.Count > 0", panelStateMethod);
        Assert.Contains("MainWindow.NoMarkdownFiles", panelStateMethod);
        Assert.Contains("MainWindow.NoMarkdownFilesHint", panelStateMethod);
    }

    [Fact]
    public void FilesSidebar_DoesNotKeepSessionHistoryState()
    {
        var code = LoadMainWindowCode();

        Assert.DoesNotContain("FileHistoryEntry", code);
        Assert.DoesNotContain("_fileHistory", code);
        Assert.DoesNotContain("AddToHistory", code);
        Assert.DoesNotContain("UpdateHistoryList", code);
        Assert.DoesNotContain("OnHistoryItemClick", code);
        Assert.DoesNotContain("OnTabHistory", code);
    }

    [Fact]
    public void SidebarToggle_AnimatesRenderTransformInsteadOfLayoutWidth()
    {
        var xaml = LoadMainWindowXaml();
        var code = LoadMainWindowCode();

        Assert.Contains("x:Name=\"SidebarTranslateTransform\" X=\"-260\"", xaml);

        var animateMethod = ExtractMethod(code, "AnimateSidebar");
        Assert.Contains("TranslateTransform.XProperty", animateMethod);
        Assert.Contains("SidebarPanel.Width =", animateMethod);
        Assert.Contains("Completed +=", animateMethod);
        Assert.DoesNotContain("FrameworkElement.WidthProperty", animateMethod);
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

    private static string LoadMainWindowCode()
    {
        var directory = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "samples",
            "WpfMarkdownEditor.Sample",
            "MainWindow.xaml.cs"));
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
        var match = Regex.Match(
            code,
            $@"private\s+(?:async\s+)?[\w<>]+\s+{Regex.Escape(methodName)}\s*\(");
        var start = match.Success ? match.Index : -1;
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
