using System.IO;
using WpfMarkdownEditor.Wpf.Services;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Translation;

public class TranslationSettingsServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly TranslationSettingsService _service;

    public TranslationSettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"translation_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new TranslationSettingsService(_testDir);
    }

    [Fact]
    public void LoadConfig_NoFile_ReturnsNull()
    {
        var config = _service.LoadConfig("Baidu");
        Assert.Null(config);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsConfig()
    {
        var config = new ProviderConfig("Baidu")
        {
            AppId = "test-app-id",
            SecretKey = "test-secret-key"
        };
        _service.SaveConfig(config);

        var loaded = _service.LoadConfig("Baidu");
        Assert.NotNull(loaded);
        Assert.Equal("Baidu", loaded.ProviderName);
        Assert.Equal("test-app-id", loaded.AppId);
        Assert.Equal("test-secret-key", loaded.SecretKey);
    }

    [Fact]
    public void SaveAndLoad_OpenAIConfig_RoundTrips()
    {
        var config = new ProviderConfig("OpenAI")
        {
            ApiEndpoint = "https://api.openai.com/v1",
            ApiKey = "sk-test-key",
            ModelName = "gpt-4o-mini"
        };
        _service.SaveConfig(config);

        var loaded = _service.LoadConfig("OpenAI");
        Assert.NotNull(loaded);
        Assert.Equal("https://api.openai.com/v1", loaded.ApiEndpoint);
        Assert.Equal("sk-test-key", loaded.ApiKey);
        Assert.Equal("gpt-4o-mini", loaded.ModelName);
    }

    [Fact]
    public void SaveConfig_EncryptsApiKey()
    {
        var config = new ProviderConfig("OpenAI")
        {
            ApiKey = "sensitive-key"
        };
        _service.SaveConfig(config);

        var filePath = Path.Combine(_testDir, "translation.json");
        var raw = File.ReadAllText(filePath);
        Assert.DoesNotContain("sensitive-key", raw);
    }

    [Fact]
    public void LoadConfig_ActiveProvider_Persists()
    {
        _service.SetActiveProvider("Baidu");
        Assert.Equal("Baidu", _service.GetActiveProvider());
    }

    [Fact]
    public void LoadConfig_NoActiveProvider_ReturnsNull()
    {
        Assert.Null(_service.GetActiveProvider());
    }

    [Fact]
    public void IsComplete_BaiduComplete_ReturnsTrue()
    {
        var config = new ProviderConfig("Baidu")
        {
            AppId = "app-id",
            SecretKey = "secret-key"
        };
        Assert.True(config.IsComplete);
    }

    [Fact]
    public void IsComplete_BaiduIncomplete_ReturnsFalse()
    {
        var config = new ProviderConfig("Baidu")
        {
            AppId = "app-id"
        };
        Assert.False(config.IsComplete);
    }

    [Fact]
    public void IsComplete_OpenAIComplete_ReturnsTrue()
    {
        var config = new ProviderConfig("OpenAI")
        {
            ApiEndpoint = "https://api.openai.com/v1",
            ApiKey = "sk-key"
        };
        Assert.True(config.IsComplete);
    }

    [Fact]
    public void IsComplete_OpenAIIncomplete_ReturnsFalse()
    {
        var config = new ProviderConfig("OpenAI")
        {
            ApiKey = "sk-key"
        };
        Assert.False(config.IsComplete);
    }

    [Fact]
    public void IsComplete_UnknownProvider_ReturnsFalse()
    {
        var config = new ProviderConfig("Unknown");
        Assert.False(config.IsComplete);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }
}
