using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

[Collection("SampleMainWindow")]
public sealed class MainWindowFileMenuStateTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.MainWindowFileMenuStateTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void FileScopedMenuItems_DisabledForEmptyDocument()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(null, localizationService, settingsService);
            try
            {
                AssertFileScopedMenuItems(window, isEnabled: false);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FileScopedMenuItems_EnabledWhenFileIsOpen()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(filePath, localizationService, settingsService);
            try
            {
                AssertFileScopedMenuItems(window, isEnabled: true);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Constructor_WithoutOpenFile_ShowsBrandOnlyTitle()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(null, localizationService, settingsService);
            try
            {
                Assert.Equal("Quillora", window.Title);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Constructor_WithInjectedSettingsDirectory_StoresRecentFilesInThatDirectory()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(filePath, localizationService, settingsService);
            try
            {
                var recentFilesService = GetPrivateField<RecentFilesService>(window, "_recentFilesService");
                var files = recentFilesService.LoadFiles();

                var entry = Assert.Single(files);
                Assert.Equal(Path.GetFullPath(filePath), entry.Path);
                Assert.True(File.Exists(Path.Combine(_directory, "recent-files.json")));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Constructor_WithOpenFile_LoadsCurrentFileDirectoryTreeAsynchronously()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            File.WriteAllText(Path.Combine(_directory, "other.md"), "# Other");
            Directory.CreateDirectory(Path.Combine(_directory, "nested"));
            var nestedFilePath = Path.Combine(_directory, "nested", "deep.md");
            File.WriteAllText(nestedFilePath, "# Deep");
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(filePath, localizationService, settingsService);
            try
            {
                WaitFor(() => GetPrivateField<string?>(window, "_workspaceFolderPath") is not null);

                Assert.Equal(Path.GetFullPath(_directory), GetPrivateField<string?>(window, "_workspaceFolderPath"));
                Assert.Equal(Visibility.Visible, Assert.IsType<TreeView>(window.FindName("FilesTree")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(window.FindName("FilesEmptyPanel")).Visibility);

                var index = GetPrivateField<Dictionary<string, WorkspaceTreeNode>>(window, "_workspaceIndex");
                var node = Assert.Contains(Path.GetFullPath(filePath), index);
                Assert.False(node.IsDirectory);
                Assert.True(node.IsSelected);
                Assert.DoesNotContain(Path.GetFullPath(nestedFilePath), index.Keys);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void LoadWorkspaceNodeChildren_LoadsExpandedDirectoryOneLevel()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            var nestedDirectory = Path.Combine(_directory, "nested");
            Directory.CreateDirectory(nestedDirectory);
            var nestedFilePath = Path.Combine(nestedDirectory, "deep.md");
            File.WriteAllText(nestedFilePath, "# Deep");
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(filePath, localizationService, settingsService);
            try
            {
                WaitFor(() => GetPrivateField<string?>(window, "_workspaceFolderPath") is not null);

                var index = GetPrivateField<Dictionary<string, WorkspaceTreeNode>>(window, "_workspaceIndex");
                var nested = Assert.Contains(Path.GetFullPath(nestedDirectory), index);
                Assert.True(nested.IsDirectory);
                Assert.False(nested.ChildrenLoaded);
                Assert.DoesNotContain(Path.GetFullPath(nestedFilePath), index.Keys);

                InvokePrivateTask(window, "LoadWorkspaceNodeChildrenAsync", nested);

                index = GetPrivateField<Dictionary<string, WorkspaceTreeNode>>(window, "_workspaceIndex");
                Assert.True(nested.ChildrenLoaded);
                var file = Assert.Contains(Path.GetFullPath(nestedFilePath), index);
                Assert.False(file.IsDirectory);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ShowInSidebarButton_ClickedWithoutOpenWorkspace_LoadsCurrentFileDirectoryAndSelectsFile()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            File.WriteAllText(Path.Combine(_directory, "other.md"), "# Other");
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);

            var window = new MainWindow(filePath, localizationService, settingsService);
            try
            {
                FindButton(window, "ShowInSidebarButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                WaitFor(() => GetPrivateField<string?>(window, "_workspaceFolderPath") is not null);

                Assert.True(GetPrivateField<bool>(window, "_sidebarOpen"));
                Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(window.FindName("FilesPanel")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(window.FindName("FilesEmptyPanel")).Visibility);
                Assert.Equal(Path.GetFullPath(_directory), GetPrivateField<string?>(window, "_workspaceFolderPath"));

                var index = GetPrivateField<Dictionary<string, WorkspaceTreeNode>>(window, "_workspaceIndex");
                var node = Assert.Contains(Path.GetFullPath(filePath), index);
                Assert.False(node.IsDirectory);
                Assert.True(node.IsSelected);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RecentFilesMenu_UsesSingleShortDisplayPathWithoutShortcutText()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);
            var window = new MainWindow(null, localizationService, settingsService);
            var path = Path.Combine(
                _directory,
                "very-long-folder-name-that-should-not-fill-the-recent-files-menu",
                "open.md");

            try
            {
                RenderRecentFilesMenu(window, [new RecentFileEntry(path, DateTime.UtcNow)]);

                var panel = Assert.IsType<StackPanel>(window.FindName("RecentFileItemsPanel"));
                var button = Assert.IsType<Button>(Assert.Single(panel.Children));

                Assert.Equal($"{Path.GetPathRoot(path)}...\\open.md", button.Content);
                Assert.Equal(string.Empty, button.Tag);
                Assert.Equal(path, button.CommandParameter);
                Assert.Equal(path, button.ToolTip);
            }
            finally
            {
                window.Close();
            }
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static void AssertFileScopedMenuItems(MainWindow window, bool isEnabled)
    {
        Assert.Equal(isEnabled, FindButton(window, "FilePropertiesButton").IsEnabled);
        Assert.Equal(isEnabled, FindButton(window, "OpenFileLocationButton").IsEnabled);
        Assert.Equal(isEnabled, FindButton(window, "ShowInSidebarButton").IsEnabled);
        Assert.Equal(isEnabled, FindButton(window, "DeleteFileButton").IsEnabled);
    }

    private static Button FindButton(MainWindow window, string name) =>
        Assert.IsType<Button>(window.FindName(name));

    private static T GetPrivateField<T>(MainWindow window, string name)
    {
        var field = typeof(MainWindow).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var value = field.GetValue(window);
        return value is null ? default! : Assert.IsType<T>(value);
    }

    private static void RenderRecentFilesMenu(MainWindow window, IReadOnlyList<RecentFileEntry> entries)
    {
        var method = typeof(MainWindow).GetMethod(
            "RenderRecentFilesMenu",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(window, [entries]);
    }

    private static void InvokePrivateTask(MainWindow window, string name, params object[] args)
    {
        var method = typeof(MainWindow).GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(window, args));
        while (!task.IsCompleted)
        {
            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.Background);
            Thread.Sleep(20);
        }

        task.GetAwaiter().GetResult();
    }

    private static void EnsureSampleApplication()
    {
        if (Application.Current != null)
            return;

        var app = new App();
        app.InitializeComponent();
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }

    private static void WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            Dispatcher.CurrentDispatcher.Invoke(
                () => { },
                DispatcherPriority.Background);
            Thread.Sleep(20);
        }

        Assert.True(condition(), "Condition was not met before the timeout.");
    }
}
