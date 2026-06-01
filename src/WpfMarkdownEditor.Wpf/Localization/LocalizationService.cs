using System.Globalization;
using System.Windows;

namespace WpfMarkdownEditor.Wpf.Localization;

public sealed class LocalizationService : IStringLocalizer
{
    private ResourceDictionary? _activeDictionary;
    private SupportedLanguage? _currentLanguage;

    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    public SupportedLanguage CurrentLanguage => _currentLanguage ?? SupportedLanguage.English;

    public static SupportedLanguage GetDefaultLanguage(CultureInfo culture) =>
        culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? SupportedLanguage.Chinese
            : SupportedLanguage.English;

    public void SetLanguage(SupportedLanguage language)
    {
        if (_currentLanguage?.Equals(language) == true)
            return;

        var application = Application.Current;
        if (application?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => SetLanguage(language));
            return;
        }

        var oldLanguage = _currentLanguage;
        _currentLanguage = language;

        if (application?.Resources != null)
            ApplyResourceDictionary(application.Resources, language);

        LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(oldLanguage, language));
    }

    public string GetString(string key)
    {
        var map = LocalizationStrings.GetMap(CurrentLanguage);
        return map.TryGetValue(key, out var value) ? value : key;
    }

    public string Format(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private void ApplyResourceDictionary(ResourceDictionary resources, SupportedLanguage language)
    {
        if (_activeDictionary != null)
            resources.MergedDictionaries.Remove(_activeDictionary);

        _activeDictionary = new ResourceDictionary
        {
            Source = new Uri(language.ResourceUri, UriKind.Absolute)
        };
        resources.MergedDictionaries.Add(_activeDictionary);
    }
}
