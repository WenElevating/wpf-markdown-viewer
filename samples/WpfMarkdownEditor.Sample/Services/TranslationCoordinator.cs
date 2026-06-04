using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;

namespace WpfMarkdownEditor.Sample.Services;

public sealed class TranslationCoordinator : IDisposable
{
    private readonly TranslationSettingsService _settingsService;
    private readonly ITranslationProviderFactory _providerFactory;
    private readonly ITranslationRunner _translationRunner;
    private CancellationTokenSource? _translationCts;

    public TranslationCoordinator(
        TranslationSettingsService settingsService,
        ITranslationProviderFactory providerFactory,
        ITranslationRunner translationRunner)
    {
        _settingsService = settingsService;
        _providerFactory = providerFactory;
        _translationRunner = translationRunner;
    }

    public bool IsTranslating { get; private set; }

    public TranslationLanguage LastTargetLanguage { get; private set; }

    public string? ActiveProvider => _settingsService.GetActiveProvider();

    public ProviderConfig? LoadActiveProviderConfig()
    {
        var activeProvider = _settingsService.GetActiveProvider();
        return activeProvider is null ? null : _settingsService.LoadConfig(activeProvider);
    }

    public void SetActiveProvider(string providerName) => _settingsService.SetActiveProvider(providerName);

    public ProviderConfig? LoadProviderConfig(string providerName) => _settingsService.LoadConfig(providerName);

    public void SaveConfig(ProviderConfig config)
    {
        _settingsService.SaveConfig(config);
        _settingsService.SetActiveProvider(config.ProviderName);
    }

    public async Task<TranslationResult> TranslateAsync(
        string markdown,
        TranslationLanguage targetLanguage,
        IProgress<TranslationProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (IsTranslating)
            throw new InvalidOperationException("A translation is already in progress.");

        LastTargetLanguage = targetLanguage;
        var config = LoadActiveProviderConfig();
        if (config?.IsComplete != true)
            throw new InvalidOperationException("The active translation provider is not configured.");

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _translationCts = linkedCts;
        IsTranslating = true;
        try
        {
            var provider = _providerFactory.Create(config);
            return await _translationRunner.TranslateMarkdownAsync(provider, markdown, targetLanguage, progress, linkedCts.Token);
        }
        finally
        {
            IsTranslating = false;
            if (ReferenceEquals(_translationCts, linkedCts))
                _translationCts = null;
        }
    }

    public void Cancel() => _translationCts?.Cancel();

    public void Dispose()
    {
        _translationCts?.Cancel();
        _translationCts?.Dispose();
        _translationCts = null;
    }
}
