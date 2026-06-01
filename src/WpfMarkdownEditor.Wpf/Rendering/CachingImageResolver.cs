using System.Collections.Concurrent;
using WpfMarkdownEditor.Core;

namespace WpfMarkdownEditor.Wpf.Rendering;

internal sealed class CachingImageResolver(IImageResolver inner) : IImageResolver
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ImageData?>>> _cache = new(StringComparer.Ordinal);

    public Task<ImageData?> ResolveImageAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult<ImageData?>(null);

        var lazy = _cache.GetOrAdd(
            url,
            static (key, resolver) => new Lazy<Task<ImageData?>>(
                () => ResolveSafelyAsync(resolver, key),
                LazyThreadSafetyMode.ExecutionAndPublication),
            inner);

        return ct.CanBeCanceled ? lazy.Value.WaitAsync(ct) : lazy.Value;
    }

    private static async Task<ImageData?> ResolveSafelyAsync(IImageResolver resolver, string url)
    {
        try
        {
            return await resolver.ResolveImageAsync(url, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }
}
