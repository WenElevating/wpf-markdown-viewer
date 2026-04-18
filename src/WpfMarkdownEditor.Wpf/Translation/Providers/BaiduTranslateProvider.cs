using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;

namespace WpfMarkdownEditor.Wpf.Translation.Providers;

public sealed class BaiduTranslateProvider : ITranslationProvider
{
    private const string ApiUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";
    private const int MaxCharsPerRequest = 6000;

    private readonly ProviderConfig? _config;
    private readonly HttpClient _httpClient;

    public string Name => "Baidu Translate";
    public bool IsConfigured => _config?.IsComplete ?? false;

    public BaiduTranslateProvider(ProviderConfig? config, HttpClient httpClient)
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
            throw new InvalidOperationException("Baidu Translate is not configured.");

        var targetCode = ToBaiduLanguageCode(targetLanguage);
        var segments = SegmentText(text, MaxCharsPerRequest);
        var translatedParts = new List<string>();
        var detectedLang = TranslationLanguage.English;

        for (var i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (i > 0)
                await Task.Delay(1100, cancellationToken);

            var (translated, detected) = await TranslateSegmentAsync(segments[i], targetCode, cancellationToken);
            translatedParts.Add(translated);
            if (i == 0) detectedLang = FromBaiduLanguageCode(detected);
        }

        return new TranslationResult(string.Join("\n\n", translatedParts), detectedLang);
    }

    private async Task<(string translated, string detected)> TranslateSegmentAsync(
        string segment, string targetCode, CancellationToken ct)
    {
        var salt = Guid.NewGuid().ToString("N")[..8];
        var sign = ComputeSignature(_config!.AppId!, segment, salt, _config.SecretKey!);

        var requestUrl = $"{ApiUrl}?q={Uri.EscapeDataString(segment)}" +
                         $"&from=auto&to={targetCode}" +
                         $"&appid={_config.AppId}&salt={salt}&sign={sign}";

        var response = await _httpClient.GetAsync(requestUrl, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error_code", out var errorCode))
        {
            var errorMsg = root.TryGetProperty("error_msg", out var msg) ? msg.GetString() : "Unknown error";
            throw new InvalidOperationException($"Baidu API error {errorCode}: {errorMsg}");
        }

        var results = root.GetProperty("trans_result").EnumerateArray();
        var translatedText = string.Join("\n", results.Select(r => r.GetProperty("dst").GetString() ?? ""));
        var detected = root.GetProperty("from").GetString() ?? "en";

        return (translatedText, detected);
    }

    internal static string ComputeSignature(string appId, string query, string salt, string secretKey)
    {
        var input = appId + query + salt + secretKey;
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static List<string> SegmentText(string text, int maxChars)
    {
        if (text.Length <= maxChars)
            return [text];

        var segments = new List<string>();
        var paragraphs = text.Split("\n\n");
        var current = new StringBuilder();

        foreach (var para in paragraphs)
        {
            if (current.Length > 0 && current.Length + 2 + para.Length > maxChars)
            {
                segments.Add(current.ToString());
                current.Clear();
            }
            else if (current.Length > 0)
            {
                current.Append("\n\n");
            }
            current.Append(para);
        }

        if (current.Length > 0)
            segments.Add(current.ToString());

        return segments;
    }

    private static string ToBaiduLanguageCode(TranslationLanguage lang) => lang switch
    {
        TranslationLanguage.English => "en",
        TranslationLanguage.Chinese => "zh",
        TranslationLanguage.Japanese => "jp",
        TranslationLanguage.Korean => "kor",
        _ => "en"
    };

    private static TranslationLanguage FromBaiduLanguageCode(string code) => code switch
    {
        "zh" or "cht" => TranslationLanguage.Chinese,
        "jp" => TranslationLanguage.Japanese,
        "kor" => TranslationLanguage.Korean,
        _ => TranslationLanguage.English
    };
}
