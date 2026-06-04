using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Wpf.Localization;

namespace WpfMarkdownEditor.Sample;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var settingsDirectory = GetSettingsDirectory();
        var filePath = e.Args.Length > 0 ? e.Args[0] : null;
        var services = new ServiceCollection()
            .AddWpfMarkdownEditorSample(settingsDirectory);
        _serviceProvider = services.BuildServiceProvider();

        var localizationSettingsService = _serviceProvider.GetRequiredService<LocalizationSettingsService>();
        var localizationService = _serviceProvider.GetRequiredService<LocalizationService>();
        var language = localizationSettingsService.LoadLanguage()
            ?? LocalizationService.GetDefaultLanguage(CultureInfo.CurrentUICulture);
        localizationService.SetLanguage(language);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.OpenStartupFile(filePath);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static string GetSettingsDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WpfMarkdownEditor.Sample");
}
