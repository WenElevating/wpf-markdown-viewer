using System.Net.Http;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Translation.Providers;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.Services;

public sealed class TranslationProviderFactoryTests
{
    [Fact]
    public void Create_Baidu_ReturnsBaiduProvider()
    {
        using var httpClient = new HttpClient();
        var factory = new TranslationProviderFactory(httpClient);
        var config = new ProviderConfig("Baidu")
        {
            AppId = "app",
            SecretKey = "secret"
        };

        var provider = factory.Create(config);

        Assert.IsType<BaiduTranslateProvider>(provider);
    }

    [Fact]
    public void Create_OpenAI_ReturnsOpenAICompatibleProvider()
    {
        using var httpClient = new HttpClient();
        var factory = new TranslationProviderFactory(httpClient);
        var config = new ProviderConfig("OpenAI")
        {
            ApiEndpoint = "https://example.test/v1",
            ApiKey = "key",
            ModelName = "test-model"
        };

        var provider = factory.Create(config);

        Assert.IsType<OpenAICompatibleProvider>(provider);
    }

    [Fact]
    public void Create_UnknownProvider_Throws()
    {
        using var httpClient = new HttpClient();
        var factory = new TranslationProviderFactory(httpClient);
        var config = new ProviderConfig("Unknown");

        Assert.Throws<InvalidOperationException>(() => factory.Create(config));
    }
}
