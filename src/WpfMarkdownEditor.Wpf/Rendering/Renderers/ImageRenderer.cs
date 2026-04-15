using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMarkdownEditor.Core;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

/// <summary>
/// Renders image blocks. Shows a placeholder initially; loads images asynchronously
/// to avoid deadlocking the UI thread.
/// </summary>
public sealed class ImageRenderer(EditorTheme theme, IImageResolver? imageResolver = null) : IBlockRenderer
{
    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var image = (ImageBlock)block;
        var container = new BlockUIContainer
        {
            Margin = new Thickness(0, 8, 0, 8),
        };

        // Try fast synchronous resolution for local/base64 only (no network)
        if (imageResolver is not null && !IsRemoteUrl(image.Url))
        {
            try
            {
                var imageData = imageResolver.ResolveImageAsync(image.Url, CancellationToken.None).GetAwaiter().GetResult();
                if (imageData is not null)
                {
                    var bitmap = CreateBitmap(imageData);
                    if (bitmap is not null)
                    {
                        container.Child = CreateImageControl(bitmap, image.Alt);
                        return container;
                    }
                }
            }
            catch { /* fall through to placeholder */ }
        }

        // Placeholder — async remote loading handled by caller
        var placeholder = new TextBlock
        {
            Text = $"[{image.Alt ?? image.Url}]",
            Foreground = new SolidColorBrush(theme.LinkColor),
            FontStyle = FontStyles.Italic,
        };

        container.Child = placeholder;
        return container;
    }

    private static bool IsRemoteUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static System.Windows.Controls.Image CreateImageControl(BitmapImage bitmap, string? alt)
    {
        var img = new System.Windows.Controls.Image
        {
            Source = bitmap,
            MaxHeight = 400,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (alt is not null)
            img.ToolTip = alt;

        return img;
    }

    private static BitmapImage? CreateBitmap(ImageData imageData)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            using (var stream = new MemoryStream(imageData.Data))
            {
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
