using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Localization;

public sealed class MainWindowLocalizationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.MainWindowLocalizationTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void LanguageMenu_PersistsSelectionAndRefreshesSelectedState()
    {
        RunOnSta(() =>
        {
            EnsureSampleApplication();
            var localizationService = new LocalizationService();
            var settingsService = new LocalizationSettingsService(_directory);
            localizationService.SetLanguage(SupportedLanguage.English);

            var window = new MainWindow(null, localizationService, settingsService);
            try
            {
                var languageList = Assert.IsType<StackPanel>(window.FindName("LanguageListPanel"));
                var chineseItem = languageList.Children
                    .OfType<RadioButton>()
                    .Single(item => Equals(item.Tag, SupportedLanguage.Chinese));

                chineseItem.IsChecked = true;
                DrainDispatcher();

                Assert.Equal(SupportedLanguage.Chinese, localizationService.CurrentLanguage);
                Assert.Equal(SupportedLanguage.Chinese, settingsService.LoadLanguage());
                Assert.All(languageList.Children.OfType<RadioButton>(), item =>
                {
                    var language = Assert.IsType<SupportedLanguage>(item.Tag);
                    Assert.Equal(language.Equals(SupportedLanguage.Chinese), item.IsChecked == true);
                });
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

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
