using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Localization;

[Collection("SampleMainWindow")]
public sealed class MainWindowLocalizationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.MainWindowLocalizationTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void LanguageMenu_PersistsSelectionAndRefreshesSelectedState()
    {
        WpfTestHost.Run(() =>
        {
            var services = new ServiceCollection();
            services.AddWpfMarkdownEditorSample(_directory);
            using var provider = services.BuildServiceProvider();
            var localizationService = provider.GetRequiredService<LocalizationService>();
            var settingsService = provider.GetRequiredService<LocalizationSettingsService>();
            localizationService.SetLanguage(SupportedLanguage.English);

            var window = provider.GetRequiredService<MainWindow>();
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

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
