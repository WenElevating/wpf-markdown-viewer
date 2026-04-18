namespace WpfMarkdownEditor.Core.Translation;

public interface ITranslationProvider
{
    string Name { get; }
    bool IsConfigured { get; }

    Task<TranslationResult> TranslateAsync(
        string text,
        TranslationLanguage targetLanguage,
        CancellationToken cancellationToken);
}
