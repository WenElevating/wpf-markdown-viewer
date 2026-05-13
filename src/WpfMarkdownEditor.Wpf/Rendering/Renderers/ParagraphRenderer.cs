using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMarkdownEditor.Core;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Inlines;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class ParagraphRenderer(EditorTheme theme, IImageResolver? imageResolver = null) : IBlockRenderer
{
    private readonly InlineRenderer _inlineRenderer = new(theme, imageResolver);

    public System.Windows.Documents.Block Render(Core.Parsing.Block block)
    {
        var para = (ParagraphBlock)block;

        // If paragraph contains only a single ImageInline, render as block-level image
        if (para.Inlines.Count == 1 && para.Inlines[0] is ImageInline img && imageResolver is not null)
        {
            // Fast synchronous path: local file via URI (no byte-array copy, fast I/O)
            var bitmap = TryLoadLocalBitmap(img.Url);
            if (bitmap is not null)
                return CreateImageContainer(bitmap, img);

            // Async path: data URIs and remote URLs via resolver
            var container = CreatePlaceholderContainer(img);
            _ = LoadBlockImageAsync(container, img, imageResolver);
            return container;
        }

        var paragraph = new Paragraph
        {
            FontFamily = theme.BodyFont,
            Foreground = new SolidColorBrush(theme.ForegroundColor),
            Margin = new Thickness(0, 0, 0, theme.ParagraphSpacing),
        };

        _inlineRenderer.RenderInlines(paragraph, para.Inlines);
        return paragraph;
    }

    private static BitmapImage? TryLoadLocalBitmap(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var fullPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppContext.BaseDirectory, url));

            if (!System.IO.File.Exists(fullPath)) return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(fullPath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private BlockUIContainer CreateImageContainer(BitmapImage bitmap, ImageInline img)
    {
        var imageControl = new System.Windows.Controls.Image
        {
            Source = bitmap,
            MaxHeight = 400,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (img.Alt is not null)
            imageControl.ToolTip = img.Alt;

        return new BlockUIContainer(imageControl) { Margin = new Thickness(0, 8, 0, 8) };
    }

    private BlockUIContainer CreatePlaceholderContainer(ImageInline img)
    {
        var placeholder = new TextBlock
        {
            Text = $"[{img.Alt ?? img.Url}]",
            Foreground = new SolidColorBrush(theme.LinkColor),
            FontStyle = FontStyles.Italic,
        };
        return new BlockUIContainer(placeholder) { Margin = new Thickness(0, 8, 0, 8) };
    }

    private static async Task LoadBlockImageAsync(BlockUIContainer container, ImageInline img, IImageResolver resolver)
    {
        try
        {
            var imageData = await resolver.ResolveImageAsync(img.Url, CancellationToken.None).ConfigureAwait(false);
            if (imageData is null) return;

            var bitmap = await Task.Run(() =>
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                using var stream = new MemoryStream(imageData.Data);
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }).ConfigureAwait(false);

            if (bitmap.PixelWidth == 0) return;

            var imageControl = new System.Windows.Controls.Image
            {
                Source = bitmap,
                MaxHeight = 400,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            if (img.Alt is not null)
                imageControl.ToolTip = img.Alt;

            await container.Dispatcher.InvokeAsync(() => container.Child = imageControl);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Leave placeholder in place
        }
    }
}
