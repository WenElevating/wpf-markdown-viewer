using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Localization;
using WpfMarkdownEditor.Wpf.Translation;

namespace WpfMarkdownEditor.Sample.Services;

public sealed class TranslationRunner : ITranslationRunner
{
    private readonly LocalizationService _localizationService;

    public TranslationRunner(LocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public Task<TranslationResult> TranslateMarkdownAsync(
        ITranslationProvider provider,
        string markdown,
        TranslationLanguage targetLanguage,
        IProgress<TranslationProgress> progress,
        CancellationToken cancellationToken)
    {
        var service = new TranslationService(provider, localizer: _localizationService);
        return service.TranslateMarkdownAsync(markdown, targetLanguage, progress, cancellationToken);
    }
}
