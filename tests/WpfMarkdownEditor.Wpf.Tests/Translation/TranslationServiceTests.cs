using System.Net.Http;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Translation;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Translation;

public class TranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_DelegatesToProvider()
    {
        var mockProvider = new MockProvider("Mock", true, new TranslationResult("translated", TranslationLanguage.English));
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });

        var result = await service.TranslateAsync("hello", TranslationLanguage.Chinese, null, CancellationToken.None);
        Assert.Equal("translated", result.TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_ReportsConnectingThenTranslating()
    {
        var progressReports = new List<TranslationProgress>();
        var mockProvider = new MockProvider("Mock", true, new TranslationResult("ok", TranslationLanguage.English));
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });

        await service.TranslateAsync("hello", TranslationLanguage.Chinese,
            new SynchronousProgress<TranslationProgress>(p => progressReports.Add(p)), CancellationToken.None);

        Assert.Equal(TranslationStage.Connecting, progressReports[0].Stage);
        Assert.Equal(TranslationStage.Translating, progressReports[1].Stage);
        Assert.Equal(TranslationStage.Completed, progressReports[^1].Stage);
    }

    [Fact]
    public async Task TranslateAsync_Cancellation_ThrowsOperationCanceled()
    {
        var mockProvider = new SlowProvider();
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.TranslateAsync("hello", TranslationLanguage.Chinese, null, cts.Token));
    }

    [Fact]
    public async Task TranslateAsync_RetriesOnTransientFailure()
    {
        var callCount = 0;
        var mockProvider = new MockProvider("Mock", true, () =>
        {
            callCount++;
            if (callCount == 1) throw new HttpRequestException("timeout");
            return new TranslationResult("ok", TranslationLanguage.English);
        });

        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 3, DelayMs = 1 });
        var result = await service.TranslateAsync("hello", TranslationLanguage.Chinese, null, CancellationToken.None);

        Assert.Equal("ok", result.TranslatedText);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task TranslateAsync_ExhaustsRetries_Throws()
    {
        var mockProvider = new MockProvider("Mock", true, () =>
            throw new HttpRequestException("persistent failure"));

        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 2, DelayMs = 1 });
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.TranslateAsync("hello", TranslationLanguage.Chinese, null, CancellationToken.None));
    }

    private sealed class MockProvider : ITranslationProvider
    {
        private readonly Func<string, TranslationResult> _factory;

        public string Name { get; }
        public bool IsConfigured { get; }
        public string? LastReceivedText { get; private set; }
        public Task<TranslationResult> TranslateAsync(string text, TranslationLanguage target, CancellationToken ct)
        {
            LastReceivedText = text;
            return Task.FromResult(_factory(text));
        }

        public MockProvider(string name, bool configured, TranslationResult result)
            => (Name, IsConfigured, _factory) = (name, configured, _ => result);

        public MockProvider(string name, bool configured, Func<TranslationResult> factory)
            => (Name, IsConfigured, _factory) = (name, configured, _ => factory());

        public MockProvider(string name, bool configured, Func<string, TranslationResult> factory)
            => (Name, IsConfigured, _factory) = (name, configured, factory);
    }

    // --- TranslateMarkdownAsync tests ---

    [Fact]
    public async Task TranslateMarkdownAsync_PreservesCodeBlocks()
    {
        var mockProvider = new MockProvider("Mock", true, (string text) =>
            new TranslationResult(text.Replace("Hello", "你好").Replace("World", "世界"), TranslationLanguage.English));
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });

        var markdown = "# Hello\n\n```csharp\nvar x = 1;\n```\n\nWorld.";
        var result = await service.TranslateMarkdownAsync(markdown, TranslationLanguage.Chinese, null, CancellationToken.None);

        Assert.Contains("```csharp\nvar x = 1;\n```", result.TranslatedText);
        Assert.Contains("你好", result.TranslatedText);
        Assert.Contains("世界", result.TranslatedText);
    }

    [Fact]
    public async Task TranslateMarkdownAsync_PreservesInlineCode()
    {
        var mockProvider = new MockProvider("Mock", true, (string text) =>
            new TranslationResult(text.Replace("Use", "使用"), TranslationLanguage.English));
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });

        var markdown = "Use `Console.WriteLine` to print.";
        var result = await service.TranslateMarkdownAsync(markdown, TranslationLanguage.Chinese, null, CancellationToken.None);

        Assert.Contains("`Console.WriteLine`", result.TranslatedText);
        Assert.Contains("使用", result.TranslatedText);
    }

    [Fact]
    public async Task TranslateMarkdownAsync_ProviderReceivesTextWithoutCodeBlocks()
    {
        var mockProvider = new MockProvider("Mock", true, (string text) =>
            new TranslationResult(text, TranslationLanguage.English));
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });

        var markdown = "# Hello\n\n```python\nprint('hi')\n```\n\nWorld.";
        await service.TranslateMarkdownAsync(markdown, TranslationLanguage.Chinese, null, CancellationToken.None);

        Assert.NotNull(mockProvider.LastReceivedText);
        // Provider should NOT receive code block content
        Assert.DoesNotContain("print('hi')", mockProvider.LastReceivedText);
        // Provider SHOULD receive the translatable text
        Assert.Contains("Hello", mockProvider.LastReceivedText);
        Assert.Contains("World.", mockProvider.LastReceivedText);
    }

    [Fact]
    public async Task TranslateMarkdownAsync_NoCodeBlocks_PreservesStructure()
    {
        var mockProvider = new MockProvider("Mock", true, (string text) =>
            new TranslationResult(text.Replace("Hello World", "你好世界").Replace("This is plain text.", "这是纯文本。"), TranslationLanguage.English));
        var service = new TranslationService(mockProvider, new RetryPolicy { MaxRetries = 0 });

        var markdown = "# Hello World\n\nThis is plain text.";
        var result = await service.TranslateMarkdownAsync(markdown, TranslationLanguage.Chinese, null, CancellationToken.None);

        Assert.Contains("# 你好世界", result.TranslatedText);
        Assert.Contains("这是纯文本。", result.TranslatedText);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _action;
        public SynchronousProgress(Action<T> action) => _action = action;
        public void Report(T value) => _action(value);
    }

    private sealed class SlowProvider : ITranslationProvider
    {
        public string Name => "Slow";
        public bool IsConfigured => true;
        public async Task<TranslationResult> TranslateAsync(string text, TranslationLanguage target, CancellationToken ct)
        {
            await Task.Delay(5000, ct);
            return new TranslationResult("", TranslationLanguage.English);
        }
    }
}
