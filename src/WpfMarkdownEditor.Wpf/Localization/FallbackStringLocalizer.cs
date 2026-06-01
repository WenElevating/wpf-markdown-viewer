using System.Globalization;

namespace WpfMarkdownEditor.Wpf.Localization;

public sealed class FallbackStringLocalizer : IStringLocalizer
{
    public static FallbackStringLocalizer Instance { get; } = new();

    private readonly IReadOnlyDictionary<string, string> _strings = LocalizationStrings.English;

    private FallbackStringLocalizer()
    {
    }

    public SupportedLanguage CurrentLanguage => SupportedLanguage.English;

    public string GetString(string key) =>
        _strings.TryGetValue(key, out var value) ? value : key;

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
}
