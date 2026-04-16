using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfMarkdownEditor.Core;
using WpfMarkdownEditor.Core.Parsing.Inlines;
using WpfMarkdownEditor.Wpf.Theming;
using CoreInline = WpfMarkdownEditor.Core.Parsing.Inline;

namespace WpfMarkdownEditor.Wpf.Rendering;

/// <summary>
/// Centralized inline element renderer. Converts Inline AST nodes to WPF Inline elements.
/// </summary>
public sealed class InlineRenderer
{
    private readonly EditorTheme _theme;
    private readonly IImageResolver? _imageResolver;

    public InlineRenderer(EditorTheme theme, IImageResolver? imageResolver = null)
    {
        _theme = theme;
        _imageResolver = imageResolver;
    }

    public void RenderInlines(Paragraph paragraph, List<CoreInline> inlines)
    {
        foreach (var inline in BatchTextInlines(inlines))
            paragraph.Inlines.Add(RenderInline(inline));
    }

    public void RenderInlines(Span span, List<CoreInline> inlines)
    {
        foreach (var inline in BatchTextInlines(inlines))
            span.Inlines.Add(RenderInline(inline));
    }

    /// <summary>
    /// Merge adjacent TextInline nodes into single TextInline to reduce WPF Run elements.
    /// </summary>
    private static List<CoreInline> BatchTextInlines(List<CoreInline> inlines)
    {
        if (inlines.Count <= 1) return inlines;

        var result = new List<CoreInline>(inlines.Count);
        var textBuffer = new System.Text.StringBuilder();

        foreach (var inline in inlines)
        {
            if (inline is TextInline ti)
            {
                textBuffer.Append(ti.Content);
            }
            else
            {
                if (textBuffer.Length > 0)
                {
                    result.Add(new TextInline { Content = textBuffer.ToString() });
                    textBuffer.Clear();
                }
                result.Add(inline);
            }
        }

        if (textBuffer.Length > 0)
            result.Add(new TextInline { Content = textBuffer.ToString() });

        return result;
    }

    private System.Windows.Documents.Inline RenderInline(CoreInline inline) => inline switch
    {
        TextInline t => new Run(t.Content),
        BoldInline b => CreateBold(b.Children),
        ItalicInline i => CreateItalic(i.Children),
        BoldItalicInline bi => CreateItalicBold(bi.Children),
        CodeInline c => new Span(new Run(c.Code))
        {
            FontFamily = _theme.CodeFont,
            FontSize = 13,
            Background = new SolidColorBrush(_theme.InlineCodeBackground),
            Foreground = new SolidColorBrush(_theme.InlineCodeForeground),
        },
        LinkInline l => CreateHyperlink(l),
        ImageInline img => RenderImageInline(img),
        StrikethroughInline s => CreateStrikethrough(s.Children),
        LineBreakInline => new LineBreak(),
        _ => new Run(string.Empty),
    };

    private Bold CreateBold(List<CoreInline> children)
    {
        var span = new Span();
        foreach (var child in children)
            span.Inlines.Add(RenderInline(child));
        return new Bold(span);
    }

    private Italic CreateItalic(List<CoreInline> children)
    {
        var span = new Span();
        foreach (var child in children)
            span.Inlines.Add(RenderInline(child));
        return new Italic(span);
    }

    private Italic CreateItalicBold(List<CoreInline> children)
    {
        var boldSpan = new Span();
        foreach (var child in children)
            boldSpan.Inlines.Add(RenderInline(child));
        return new Italic(new Bold(boldSpan));
    }

    private Span CreateStrikethrough(List<CoreInline> children)
    {
        var span = new Span();
        foreach (var child in children)
            span.Inlines.Add(RenderInline(child));
        span.TextDecorations = TextDecorations.Strikethrough;
        return span;
    }

    private System.Windows.Documents.Inline RenderImageInline(ImageInline img)
    {
        if (_imageResolver is not null)
        {
            try
            {
                var imageData = _imageResolver.ResolveImageAsync(img.Url, CancellationToken.None).GetAwaiter().GetResult();
                if (imageData is not null)
                {
                    var bitmap = CreateBitmap(imageData);
                    if (bitmap is not null)
                    {
                        var imageControl = new System.Windows.Controls.Image
                        {
                            Source = bitmap,
                            MaxHeight = 300,
                            Stretch = Stretch.Uniform,
                            StretchDirection = StretchDirection.DownOnly,
                        };
                        if (img.Alt is not null)
                            imageControl.ToolTip = img.Alt;
                        return new InlineUIContainer(imageControl) { BaselineAlignment = BaselineAlignment.Bottom };
                    }
                }
            }
            catch { /* fall through to placeholder */ }
        }

        return new Run($"[{img.Alt ?? img.Url}]")
        {
            Foreground = new SolidColorBrush(_theme.LinkColor),
        };
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

    private Hyperlink CreateHyperlink(LinkInline link)
    {
        var span = new Span();
        foreach (var child in link.Children)
            span.Inlines.Add(RenderInline(child));

        var hyperlink = new Hyperlink(span)
        {
            Foreground = new SolidColorBrush(_theme.LinkColor),
            TextDecorations = null,
        };

        if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
        {
            hyperlink.NavigateUri = uri;
            hyperlink.RequestNavigate += (s, e) =>
            {
                Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
                e.Handled = true;
            };
        }

        return hyperlink;
    }
}
