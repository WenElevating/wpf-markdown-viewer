namespace WpfMarkdownEditor.Core.Translation;

public enum TranslationLanguage
{
    English,
    Chinese,
    Japanese,
    Korean
}

public static class TranslationLanguageExtensions
{
    public static string DisplayName(this TranslationLanguage language) => language switch
    {
        TranslationLanguage.English => "English",
        TranslationLanguage.Chinese => "中文",
        TranslationLanguage.Japanese => "日本語",
        TranslationLanguage.Korean => "한국어",
        _ => language.ToString()
    };
}
