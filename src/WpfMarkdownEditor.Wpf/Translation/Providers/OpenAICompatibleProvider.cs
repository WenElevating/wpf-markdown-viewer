using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;

namespace WpfMarkdownEditor.Wpf.Translation.Providers;

public sealed class OpenAICompatibleProvider : ITranslationProvider
{
    private readonly ProviderConfig? _config;
    private readonly HttpClient _httpClient;

    public string Name => "OpenAI Compatible";
    public bool IsConfigured => _config?.IsComplete ?? false;

    public OpenAICompatibleProvider(ProviderConfig? config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<TranslationResult> TranslateAsync(
        string text,
        TranslationLanguage targetLanguage,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI Compatible provider is not configured.");

        var endpoint = _config!.ApiEndpoint!.TrimEnd('/');
        var url = $"{endpoint}/chat/completions";

        var requestBody = new
        {
            model = _config.ModelName ?? "gpt-4o-mini",
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt(targetLanguage) },
                new { role = "user", content = text }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = TryExtractErrorMessage(responseBody) ?? $"HTTP {response.StatusCode}";
            throw new InvalidOperationException($"Translation failed: {errorMsg}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var translatedText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        var detectedLang = DetectSourceLanguage(text);
        return new TranslationResult(translatedText, detectedLang);
    }

    internal static string BuildSystemPrompt(TranslationLanguage targetLanguage) =>
        $"""
        You are a professional translator. Translate the following text to {targetLanguage.DisplayName()}.

        CRITICAL RULES:
        1. Output EXACTLY the same number of lines as the input. Do NOT merge, split, add, or remove any lines.
        2. Preserve ALL tokens in the format XBS, XBE, XIS, XIE, XCS, XCE, XLS, XLE, XURL (followed by digits) exactly as-is. These are protected markers — never modify, translate, or remove them.
        3. Translate ONLY the human-readable text on each line. Do not translate tokens or URLs.
        4. Output ONLY the translated text. No explanations, no quotes, no extra content.
        """;

    private static string? TryExtractErrorMessage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString();
        }
        catch
        {
            return null;
        }
    }

    private static TranslationLanguage DetectSourceLanguage(string text)
    {
        var cjkCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var hiraganaCount = text.Count(c => c >= 0x3040 && c <= 0x309F);
        var hangulCount = text.Count(c => c >= 0xAC00 && c <= 0xD7AF);

        if (hiraganaCount > 5) return TranslationLanguage.Japanese;
        if (hangulCount > 5) return TranslationLanguage.Korean;
        if (cjkCount > 5) return TranslationLanguage.Chinese;
        return TranslationLanguage.English;
    }
}
