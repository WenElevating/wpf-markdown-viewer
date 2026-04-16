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
            var blockImage = TryRenderBlockImage(img);
            if (blockImage is not null)
                return blockImage;
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

    private BlockUIContainer? TryRenderBlockImage(ImageInline img)
    {
        try
        {
            BitmapImage? bitmap = null;
            var url = img.Url;

            // For local files, load directly via URI (most reliable)
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, url));

                if (System.IO.File.Exists(fullPath))
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
            }

            // Fallback: load via IImageResolver (byte array)
            if (bitmap is null && imageResolver is not null)
            {
                var imageData = imageResolver.ResolveImageAsync(url, CancellationToken.None)
                    .GetAwaiter().GetResult();
                if (imageData is not null)
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    using (var stream = new MemoryStream(imageData.Data))
                    {
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze();
                }
            }

            if (bitmap is null || bitmap.PixelWidth == 0) return null;

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

            return new BlockUIContainer(imageControl)
            {
                Margin = new Thickness(0, 8, 0, 8),
            };
        }
        catch
        {
            return null;
        }
    }
}
