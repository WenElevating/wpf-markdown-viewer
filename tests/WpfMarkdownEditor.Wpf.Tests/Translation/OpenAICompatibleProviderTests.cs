using System.Net;
using System.Net.Http;
using System.Text.Json;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Translation.Providers;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Translation;

public class OpenAICompatibleProviderTests
{
    [Fact]
    public void Name_ReturnsOpenAICompatible()
    {
        var provider = CreateProvider();
        Assert.Equal("OpenAI Compatible", provider.Name);
    }

    [Fact]
    public void IsConfigured_WithValidConfig_ReturnsTrue()
    {
        var provider = CreateProvider("https://api.openai.com/v1", "sk-test", "gpt-4o-mini");
        Assert.True(provider.IsConfigured);
    }

    [Fact]
    public void IsConfigured_MissingEndpoint_ReturnsFalse()
    {
        var provider = CreateProvider(null, "sk-test", "gpt-4o-mini");
        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public void IsConfigured_MissingApiKey_ReturnsFalse()
    {
        var provider = CreateProvider("https://api.openai.com/v1", null, "gpt-4o-mini");
        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public void Presets_ContainsExpectedServices()
    {
        Assert.True(OpenAIPresets.All.Count >= 4);
        Assert.Contains(OpenAIPresets.All, p => p.Name == "Tongyi Qwen");
        Assert.Contains(OpenAIPresets.All, p => p.Name == "Zhipu GLM");
        Assert.Contains(OpenAIPresets.All, p => p.Name == "DeepSeek");
        Assert.Contains(OpenAIPresets.All, p => p.Name == "OpenAI");
    }

    [Fact]
    public void BuildSystemPrompt_ContainsTargetLanguage()
    {
        var prompt = OpenAICompatibleProvider.BuildSystemPrompt(TranslationLanguage.Chinese);
        Assert.Contains("中文", prompt);
        Assert.Contains("XBS", prompt);
    }

    [Fact]
    public async Task TranslateAsync_ReturnsTranslatedText()
    {
        var handler = new MockHttpHandler(req =>
        {
            var response = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new { content = "# 你好世界\n\n这是翻译后的文本。" }
                    }
                }
            };
            return JsonSerializer.Serialize(response);
        });

        var provider = CreateProvider("https://api.openai.com/v1", "sk-test", "gpt-4o-mini", handler);
        var result = await provider.TranslateAsync("# Hello World\n\nThis is translated text.", TranslationLanguage.Chinese, CancellationToken.None);

        Assert.Equal("# 你好世界\n\n这是翻译后的文本。", result.TranslatedText);
        Assert.Equal(TranslationLanguage.English, result.DetectedSourceLanguage);
    }

    [Fact]
    public async Task TranslateAsync_SendsCorrectRequestFormat()
    {
        HttpRequestMessage? captured = null;
        var handler = new MockHttpHandler(req =>
        {
            captured = req;
            var response = new { choices = new[] { new { message = new { content = "翻译" } } } };
            return JsonSerializer.Serialize(response);
        });

        var provider = CreateProvider("https://api.openai.com/v1", "sk-test", "gpt-4o-mini", handler);
        await provider.TranslateAsync("test", TranslationLanguage.Japanese, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("https://api.openai.com/v1/chat/completions", captured.RequestUri?.ToString());
        Assert.Equal("Bearer sk-test", captured.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task TranslateAsync_ApiError_Throws()
    {
        var handler = new MockHttpHandlerWithStatus(req =>
        {
            return (HttpStatusCode.Unauthorized, JsonSerializer.Serialize(new { error = new { message = "Invalid API key" } }));
        });

        var provider = CreateProvider("https://api.openai.com/v1", "bad-key", "gpt-4o-mini", handler);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TranslateAsync("test", TranslationLanguage.Chinese, CancellationToken.None));
        Assert.Contains("Invalid API key", ex.Message);
    }

    private static OpenAICompatibleProvider CreateProvider(
        string? endpoint = null, string? apiKey = null, string? model = null, HttpMessageHandler? handler = null)
    {
        var config = (endpoint != null && apiKey != null)
            ? new ProviderConfig("OpenAI") { ApiEndpoint = endpoint, ApiKey = apiKey, ModelName = model }
            : null;
        var httpClient = handler != null ? new HttpClient(handler) : new HttpClient();
        return new OpenAICompatibleProvider(config, httpClient);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _respond;
        public MockHttpHandler(Func<HttpRequestMessage, string> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = _respond(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private sealed class MockHttpHandlerWithStatus : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _respond;
        public MockHttpHandlerWithStatus(Func<HttpRequestMessage, (HttpStatusCode, string)> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var (status, body) = _respond(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
