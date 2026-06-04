using System.IO;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Wpf.Services;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.Services;

public sealed class TranslationCoordinatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.TranslationCoordinatorTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TranslateAsync_UsesFactoryAndRunner()
    {
        var settings = CreateConfiguredSettings();
        var providerFactory = new FakeTranslationProviderFactory();
        var runner = new FakeTranslationRunner();
        var coordinator = new TranslationCoordinator(settings, providerFactory, runner);

        var result = await coordinator.TranslateAsync(
            "# Source",
            TranslationLanguage.Chinese,
            new Progress<TranslationProgress>());

        Assert.Equal("translated", result.TranslatedText);
        Assert.Equal(TranslationLanguage.Chinese, coordinator.LastTargetLanguage);
        Assert.Equal(1, providerFactory.CreateCount);
        Assert.Equal(1, runner.RunCount);
        Assert.False(coordinator.IsTranslating);
    }

    [Fact]
    public async Task TranslateAsync_WhenAlreadyTranslating_DoesNotCreateProviderOrRunner()
    {
        var settings = CreateConfiguredSettings();
        var providerFactory = new FakeTranslationProviderFactory();
        var runner = new BlockingTranslationRunner();
        var coordinator = new TranslationCoordinator(settings, providerFactory, runner);
        var first = coordinator.TranslateAsync("# Source", TranslationLanguage.Chinese, new Progress<TranslationProgress>());

        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.TranslateAsync("# Source", TranslationLanguage.English, new Progress<TranslationProgress>()));

        Assert.Contains("already", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, providerFactory.CreateCount);
        Assert.Equal(1, runner.RunCount);

        runner.Release.SetResult();
        await first;
        Assert.False(coordinator.IsTranslating);
    }

    [Fact]
    public async Task Cancel_CancelsCurrentTranslation()
    {
        var settings = CreateConfiguredSettings();
        var runner = new BlockingTranslationRunner();
        var coordinator = new TranslationCoordinator(settings, new FakeTranslationProviderFactory(), runner);
        var task = coordinator.TranslateAsync("# Source", TranslationLanguage.Chinese, new Progress<TranslationProgress>());

        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Dispose_CancelsCurrentTranslationAndClearsState()
    {
        var settings = CreateConfiguredSettings();
        var runner = new BlockingTranslationRunner();
        var coordinator = new TranslationCoordinator(settings, new FakeTranslationProviderFactory(), runner);
        var task = coordinator.TranslateAsync("# Source", TranslationLanguage.Chinese, new Progress<TranslationProgress>());

        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.False(coordinator.IsTranslating);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private TranslationSettingsService CreateConfiguredSettings()
    {
        Directory.CreateDirectory(_directory);
        var settings = new TranslationSettingsService(_directory);
        settings.SaveConfig(new ProviderConfig("OpenAI")
        {
            ApiEndpoint = "https://example.test/v1",
            ApiKey = "key",
            ModelName = "model"
        });
        settings.SetActiveProvider("OpenAI");
        return settings;
    }

    private sealed class FakeTranslationProviderFactory : ITranslationProviderFactory
    {
        public int CreateCount { get; private set; }

        public ITranslationProvider Create(ProviderConfig config)
        {
            CreateCount++;
            return new FakeTranslationProvider();
        }
    }

    private sealed class FakeTranslationRunner : ITranslationRunner
    {
        public int RunCount { get; private set; }

        public Task<TranslationResult> TranslateMarkdownAsync(
            ITranslationProvider provider,
            string markdown,
            TranslationLanguage targetLanguage,
            IProgress<TranslationProgress> progress,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new TranslationResult("translated", TranslationLanguage.English));
        }
    }

    private sealed class BlockingTranslationRunner : ITranslationRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCount { get; private set; }

        public async Task<TranslationResult> TranslateMarkdownAsync(
            ITranslationProvider provider,
            string markdown,
            TranslationLanguage targetLanguage,
            IProgress<TranslationProgress> progress,
            CancellationToken cancellationToken)
        {
            RunCount++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new TranslationResult("translated", TranslationLanguage.English);
        }
    }

    private sealed class FakeTranslationProvider : ITranslationProvider
    {
        public string Name => "Fake";

        public bool IsConfigured => true;

        public Task<TranslationResult> TranslateAsync(
            string text,
            TranslationLanguage targetLanguage,
            CancellationToken cancellationToken)
            => Task.FromResult(new TranslationResult(text, TranslationLanguage.English));
    }
}
