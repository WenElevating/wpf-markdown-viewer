using System.IO;
using System.Text.Json;
using WpfMarkdownEditor.Wpf.Localization;

namespace WpfMarkdownEditor.Sample;

public sealed class LocalizationSettingsService
{
    private const string FileName = "localization-settings.json";
    private readonly string _settingsDirectory;

    public LocalizationSettingsService(string settingsDirectory)
    {
        _settingsDirectory = settingsDirectory;
    }

    public string SettingsDirectory => _settingsDirectory;

    public SupportedLanguage? LoadLanguage()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<LocalizationSettingsModel>(json);
            return SupportedLanguage.FromCode(model?.Language);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void SaveLanguage(SupportedLanguage language)
    {
        Directory.CreateDirectory(_settingsDirectory);
        var json = JsonSerializer.Serialize(new LocalizationSettingsModel(language.Code));
        File.WriteAllText(GetSettingsPath(), json);
    }

    private string GetSettingsPath() => Path.Combine(_settingsDirectory, FileName);

    private sealed record LocalizationSettingsModel(string? Language);
}
