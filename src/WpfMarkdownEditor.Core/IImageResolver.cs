namespace WpfMarkdownEditor.Core;

/// <summary>
/// Resolves image URLs to byte data. Defined in Core, implemented in WPF.
/// </summary>
public interface IImageResolver
{
    Task<ImageData?> ResolveImageAsync(string url, CancellationToken ct);
}

/// <summary>
/// Raw image data DTO, framework-agnostic.
/// </summary>
public sealed class ImageData
{
    public required byte[] Data { get; init; }
    public required string Format { get; init; } // "png", "jpg", "gif", "svg", etc.
}
