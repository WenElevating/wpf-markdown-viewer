namespace WpfMarkdownEditor.Wpf.Localization;

public interface IStringLocalizer
{
    SupportedLanguage CurrentLanguage { get; }

    string GetString(string key);

    string Format(string key, params object[] args);
}
