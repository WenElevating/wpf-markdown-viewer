using System.Net.Http;
using WpfMarkdownEditor.Core.Translation;

namespace WpfMarkdownEditor.Wpf.Translation;

public sealed class TranslationService
{
    private readonly ITranslationProvider _provider;
    private readonly RetryPolicy _retryPolicy;

    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(300);

    public TranslationService(ITranslationProvider provider, RetryPolicy? retryPolicy = null)
    {
        _provider = provider;
        _retryPolicy = retryPolicy ?? new RetryPolicy();
    }

    public ITranslationProvider CurrentProvider => _provider;

    /// <summary>
    /// Translates markdown content using template-based extraction.
    /// Preserves all markdown formatting (headings, lists, tables, code blocks, inline markers).
    /// Renders in preview only — does not modify editor content.
    /// </summary>
    public async Task<TranslationResult> TranslateMarkdownAsync(
        string markdown,
        TranslationLanguage targetLanguage,
        IProgress<TranslationProgress>? progress,
        CancellationToken ct)
    {
        // 1. Extract translatable text into a template
        var (plainText, template, inlineTokens) = MarkdownSegmentExtractor.Extract(markdown);

        // 2. Translate the plain text
        var result = await TranslateAsync(plainText, targetLanguage, progress, ct);

        // 3. Reconstruct markdown from template + translated text
        var restoredMarkdown = MarkdownSegmentExtractor.Reconstruct(template, result.TranslatedText, inlineTokens);

        return new TranslationResult(restoredMarkdown, result.DetectedSourceLanguage);
    }

    public async Task<TranslationResult> TranslateAsync(
        string text,
        TranslationLanguage targetLanguage,
        IProgress<TranslationProgress>? progress,
        CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(TotalTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var effectiveCt = linkedCts.Token;

        Report(progress, TranslationStage.Connecting, $"Connecting to {_provider.Name}...");
        Report(progress, TranslationStage.Translating, "Translating...");

        Exception? lastException = null;

        for (var attempt = 0; attempt <= _retryPolicy.MaxRetries; attempt++)
        {
            effectiveCt.ThrowIfCancellationRequested();

            try
            {
                if (attempt > 0)
                {
                    Report(progress, TranslationStage.Translating, $"Retrying... (attempt {attempt + 1})");
                    await Task.Delay(_retryPolicy.DelayMs * (1 << (attempt - 1)), effectiveCt);
                }

                var result = await _provider.TranslateAsync(text, targetLanguage, effectiveCt);
                Report(progress, TranslationStage.Completed, "Translation completed");
                return result;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException("Translation timed out. Please check your network or try a shorter document.");
            }
            catch (OperationCanceledException) { throw; }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                if (attempt >= _retryPolicy.MaxRetries) throw;
            }
            catch (InvalidOperationException ex) when (IsTransientError(ex))
            {
                lastException = ex;
                if (attempt >= _retryPolicy.MaxRetries) throw;
            }
        }

        throw lastException!;
    }

    private static void Report(IProgress<TranslationProgress>? progress, TranslationStage stage, string message)
        => progress?.Report(new TranslationProgress(stage, 0, 0, message));

    private static bool IsTransientError(InvalidOperationException ex) =>
        ex.Message.Contains("54003") ||
        ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
}
