using WpfMarkdownEditor.Core.Translation;

namespace WpfMarkdownEditor.Sample.Services;

public interface ITranslationRunner
{
    Task<TranslationResult> TranslateMarkdownAsync(
        ITranslationProvider provider,
        string markdown,
        TranslationLanguage targetLanguage,
        IProgress<TranslationProgress> progress,
        CancellationToken cancellationToken);
}
