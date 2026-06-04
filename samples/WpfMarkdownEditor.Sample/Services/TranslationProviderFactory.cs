using System.Net.Http;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Translation.Providers;

namespace WpfMarkdownEditor.Sample.Services;

public sealed class TranslationProviderFactory : ITranslationProviderFactory, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public TranslationProviderFactory()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(120) }, ownsHttpClient: true)
    {
    }

    public TranslationProviderFactory(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private TranslationProviderFactory(HttpClient httpClient, bool ownsHttpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public ITranslationProvider Create(ProviderConfig config)
    {
        return config.ProviderName switch
        {
            "Baidu" => new BaiduTranslateProvider(config, _httpClient),
            "OpenAI" => new OpenAICompatibleProvider(config, _httpClient),
            _ => throw new InvalidOperationException($"Unsupported translation provider '{config.ProviderName}'.")
        };
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
