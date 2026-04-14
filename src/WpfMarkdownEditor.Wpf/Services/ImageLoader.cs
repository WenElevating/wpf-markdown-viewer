using System.IO;
using System.Net.Http;
using WpfMarkdownEditor.Core;

namespace WpfMarkdownEditor.Wpf.Services;

/// <summary>
/// Resolves image URLs to raw byte data. Supports local files, base64 data URIs, and remote URLs.
/// Implements <see cref="IImageResolver"/> from Core.
/// </summary>
public sealed class ImageLoader : IImageResolver, IDisposable
{
    private readonly HttpClient _http;
    private readonly string? _baseDirectory;
    private readonly string _tempDir;
    private bool _disposed;

    public ImageLoader(string? baseDirectory = null)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _baseDirectory = baseDirectory;
        _tempDir = Path.Combine(Path.GetTempPath(), "WpfMarkdownEditor", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public async Task<ImageData?> ResolveImageAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            // Base64 data URI: data:image/png;base64,...
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return ResolveDataUri(url);

            // Remote URL
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return await ResolveRemoteAsync(url, ct);

            // Local file (relative to base directory or absolute)
            return ResolveLocal(url);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return null;
        }
    }

    private static ImageData? ResolveDataUri(string url)
    {
        var separatorIndex = url.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
        if (separatorIndex < 0) return null;

        var mimePart = url[5..separatorIndex]; // skip "data:"
        var format = mimePart switch
        {
            "image/png" => "png",
            "image/jpeg" or "image/jpg" => "jpg",
            "image/gif" => "gif",
            "image/svg+xml" => "svg",
            "image/webp" => "webp",
            _ => "png"
        };

        var base64Data = url[(separatorIndex + 8)..];
        if (base64Data.Length == 0) return null;

        var data = Convert.FromBase64String(base64Data);
        return new ImageData { Data = data, Format = format };
    }

    private async Task<ImageData?> ResolveRemoteAsync(string url, CancellationToken ct)
    {
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsByteArrayAsync(ct);
        var format = InferFormatFromUrl(url) ?? InferFormatFromContentType(response.Content.Headers.ContentType?.MediaType);
        return new ImageData { Data = data, Format = format ?? "png" };
    }

    private ImageData? ResolveLocal(string url)
    {
        var path = url;
        if (!Path.IsPathRooted(url) && _baseDirectory is not null)
            path = Path.Combine(_baseDirectory, url);

        if (!File.Exists(path)) return null;

        var data = File.ReadAllBytes(path);
        var format = InferFormatFromUrl(url) ?? "png";
        return new ImageData { Data = data, Format = format };
    }

    private static string? InferFormatFromUrl(string? url)
    {
        if (url is null) return null;
        var ext = Path.GetExtension(url).ToLowerInvariant();
        return ext switch
        {
            ".png" => "png",
            ".jpg" or ".jpeg" => "jpg",
            ".gif" => "gif",
            ".svg" => "svg",
            ".webp" => "webp",
            ".bmp" => "bmp",
            _ => null
        };
    }

    private static string? InferFormatFromContentType(string? contentType)
    {
        return contentType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            "image/gif" => "gif",
            "image/svg+xml" => "svg",
            "image/webp" => "webp",
            _ => null
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }
}
