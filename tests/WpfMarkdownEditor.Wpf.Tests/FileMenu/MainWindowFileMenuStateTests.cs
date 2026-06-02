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
    public void FileScopedMenuItems_DisabledForUntitledDocument()
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

    private static void RenderRecentFilesMenu(MainWindow window, IReadOnlyList<RecentFileEntry> entries)
    {
        var method = typeof(MainWindow).GetMethod(
            "RenderRecentFilesMenu",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(window, [entries]);
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
}
