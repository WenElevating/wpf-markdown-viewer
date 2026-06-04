using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using WpfMarkdownEditor.Sample.ViewModels;
using WpfMarkdownEditor.Wpf.Localization;
using WpfMarkdownEditor.Wpf.Services;

namespace WpfMarkdownEditor.Sample.Services;

public static class SampleServiceCollectionExtensions
{
    public static IServiceCollection AddWpfMarkdownEditorSample(
        this IServiceCollection services,
        string settingsDirectory)
    {
        services.AddSingleton<LocalizationService>();
        services.AddSingleton(_ => new LocalizationSettingsService(settingsDirectory));
        services.AddSingleton(_ => new TranslationSettingsService(settingsDirectory));
        services.AddSingleton(_ => new RecentFilesService(settingsDirectory));
        services.AddSingleton<FolderWorkspaceService>();
        services.AddSingleton<FileOperationService>();
        services.AddSingleton<HtmlExportService>();
        services.AddSingleton<QuickOpenService>();
        services.AddSingleton<DocumentSessionService>();
        services.AddTransient<WorkspaceViewModel>();
        services.AddTransient<RecentFilesMenuViewModel>();
        services.AddTransient<MainWindowViewModel>();
        services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(120) });
        services.AddSingleton<ITranslationProviderFactory, TranslationProviderFactory>();
        services.AddTransient<ITranslationRunner, TranslationRunner>();
        services.AddTransient<TranslationCoordinator>();
        services.AddTransient<Func<MainWindow>>(provider => provider.GetRequiredService<MainWindow>);
        services.AddTransient<MainWindow>();
        return services;
    }
}
