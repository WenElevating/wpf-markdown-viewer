using System.Globalization;
using System.IO;
using System.Windows;
using WpfMarkdownEditor.Wpf.Localization;

namespace WpfMarkdownEditor.Sample;

public partial class App : Application
{
    private readonly LocalizationService _localizationService = new();
    private LocalizationSettingsService? _localizationSettingsService;

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        _localizationSettingsService = new LocalizationSettingsService(GetSettingsDirectory());
        var language = _localizationSettingsService.LoadLanguage()
            ?? LocalizationService.GetDefaultLanguage(CultureInfo.CurrentUICulture);
        _localizationService.SetLanguage(language);

        var filePath = e.Args.Length > 0 ? e.Args[0] : null;
        var mainWindow = new MainWindow(filePath, _localizationService, _localizationSettingsService);
        mainWindow.Show();
    }

    private static string GetSettingsDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WpfMarkdownEditor.Sample");
}
