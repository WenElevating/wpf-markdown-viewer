using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Sample.ViewModels;
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
        WpfTestHost.Run(() =>
        {
            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
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
        WpfTestHost.Run(() =>
        {
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");

            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
            window.OpenStartupFile(filePath);
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
        WpfTestHost.Run(() =>
        {
            using var provider = CreateProvider();
            var localizationService = provider.GetRequiredService<LocalizationService>();
            localizationService.SetLanguage(SupportedLanguage.English);
            var window = provider.GetRequiredService<MainWindow>();
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
        WpfTestHost.Run(() =>
        {
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");

            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
            window.OpenStartupFile(filePath);
            try
            {
                var recentFilesService = provider.GetRequiredService<RecentFilesService>();
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
        WpfTestHost.Run(() =>
        {
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            File.WriteAllText(Path.Combine(_directory, "other.md"), "# Other");
            Directory.CreateDirectory(Path.Combine(_directory, "nested"));
            File.WriteAllText(Path.Combine(_directory, "nested", "deep.md"), "# Deep");

            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
            window.OpenStartupFile(filePath);
            try
            {
                WaitFor(() => Assert.IsType<TreeView>(window.FindName("FilesTree")).Items.Count > 0);

                Assert.Equal(Visibility.Visible, Assert.IsType<TreeView>(window.FindName("FilesTree")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(window.FindName("FilesEmptyPanel")).Visibility);
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
        WpfTestHost.Run(() =>
        {
            Directory.CreateDirectory(_directory);
            var filePath = Path.Combine(_directory, "open.md");
            File.WriteAllText(filePath, "# Open");
            File.WriteAllText(Path.Combine(_directory, "other.md"), "# Other");

            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
            window.OpenStartupFile(filePath);
            try
            {
                FindButton(window, "ShowInSidebarButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
                WaitFor(() => viewModel.IsSidebarOpen);

                Assert.True(viewModel.IsSidebarOpen);
                Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(window.FindName("FilesPanel")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(window.FindName("FilesEmptyPanel")).Visibility);
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
        WpfTestHost.Run(() =>
        {
            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
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

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddWpfMarkdownEditorSample(_directory);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<LocalizationService>().SetLanguage(SupportedLanguage.English);
        return provider;
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
