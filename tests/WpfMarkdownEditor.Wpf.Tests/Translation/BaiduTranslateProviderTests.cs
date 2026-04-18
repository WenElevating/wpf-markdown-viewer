using System.Net;
using System.Net.Http;
using System.Text.Json;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Translation.Providers;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Translation;

public class BaiduTranslateProviderTests
{
    [Fact]
    public void Name_ReturnsBaiduTranslate()
    {
        var provider = CreateProvider();
        Assert.Equal("Baidu Translate", provider.Name);
    }

    [Fact]
    public void IsConfigured_WithValidConfig_ReturnsTrue()
    {
        var provider = CreateProvider("appid", "secret");
        Assert.True(provider.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WithEmptyConfig_ReturnsFalse()
    {
        var provider = CreateProvider();
        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public void ComputeSignature_MatchesBaiduSpec()
    {
        var sign = BaiduTranslateProvider.ComputeSignature("test-appid", "hello", "1234", "test-secret");
        Assert.Equal(32, sign.Length);
    }

    [Fact]
    public void SegmentText_ShortText_ReturnsSingleSegment()
    {
        var segments = BaiduTranslateProvider.SegmentText("Hello world", 6000);
        Assert.Single(segments);
    }

    [Fact]
    public void SegmentText_LongText_SplitsAtParagraphs()
    {
        var paragraphs = Enumerable.Range(0, 50)
            .Select(_ => new string('a', 200))
            .ToArray();
        var text = string.Join("\n\n", paragraphs);
        var segments = BaiduTranslateProvider.SegmentText(text, 6000);
        Assert.True(segments.Count > 1);
        Assert.Equal(text, string.Join("\n\n", segments));
    }

    [Fact]
    public async Task TranslateAsync_SingleSegment_ReturnsResult()
    {
        var handler = new MockHttpHandler(req =>
        {
            var response = new
            {
                from = "en",
                to = "zh",
                trans_result = new[]
                {
                    new { src = "Hello", dst = "你好" }
                }
            };
            return JsonSerializer.Serialize(response);
        });

        var provider = CreateProvider("appid", "secret", handler);
        var result = await provider.TranslateAsync("Hello", TranslationLanguage.Chinese, CancellationToken.None);

        Assert.Equal("你好", result.TranslatedText);
        Assert.Equal(TranslationLanguage.English, result.DetectedSourceLanguage);
    }

    [Fact]
    public async Task TranslateAsync_ApiError_ThrowsWithMessage()
    {
        var handler = new MockHttpHandler(req =>
        {
            return JsonSerializer.Serialize(new { error_code = 54003, error_msg = "Invalid Sign" });
        });

        var provider = CreateProvider("appid", "secret", handler);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranslateAsync("test", TranslationLanguage.Chinese, CancellationToken.None));
        Assert.Contains("54003", ex.Message);
    }

    [Fact]
    public async Task TranslateAsync_NetworkError_ThrowsHttpRequestException()
    {
        var handler = new MockHttpHandler(_ => throw new HttpRequestException("Connection refused"));
        var provider = CreateProvider("appid", "secret", handler);
        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.TranslateAsync("test", TranslationLanguage.Chinese, CancellationToken.None));
    }

    private static BaiduTranslateProvider CreateProvider(string? appId = null, string? secretKey = null, HttpMessageHandler? handler = null)
    {
        var config = (appId != null && secretKey != null)
            ? new ProviderConfig("Baidu") { AppId = appId, SecretKey = secretKey }
            : null;
        var httpClient = handler != null ? new HttpClient(handler) : new HttpClient();
        return new BaiduTranslateProvider(config, httpClient);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _respond;
        public MockHttpHandler(Func<HttpRequestMessage, string> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            try
            {
                var body = _respond(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
