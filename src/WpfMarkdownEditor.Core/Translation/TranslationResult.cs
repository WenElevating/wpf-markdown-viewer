namespace WpfMarkdownEditor.Core.Translation;

public sealed record TranslationResult(
    string TranslatedText,
    TranslationLanguage DetectedSourceLanguage);
