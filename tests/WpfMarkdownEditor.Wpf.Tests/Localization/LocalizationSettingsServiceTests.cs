using System.IO;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Localization;

public sealed class LocalizationSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.LocalizationTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_PersistsLanguageCode()
    {
        var service = new LocalizationSettingsService(_directory);

        service.SaveLanguage(SupportedLanguage.Chinese);

        Assert.Equal(SupportedLanguage.Chinese, service.LoadLanguage());
        Assert.Contains("zh-CN", File.ReadAllText(Path.Combine(_directory, "localization-settings.json")));
    }

    [Fact]
    public void LoadLanguage_ReturnsNullForMalformedContent()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "localization-settings.json"), "{ not-json");

        var service = new LocalizationSettingsService(_directory);

        Assert.Null(service.LoadLanguage());
    }

    [Fact]
    public void LoadLanguage_ReturnsNullForUnsupportedCode()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "localization-settings.json"), "{\"language\":\"fr-FR\"}");

        var service = new LocalizationSettingsService(_directory);

        Assert.Null(service.LoadLanguage());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
