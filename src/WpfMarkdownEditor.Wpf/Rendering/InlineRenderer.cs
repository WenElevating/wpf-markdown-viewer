using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly Action? _requestLayoutRefresh;

    public InlineRenderer(EditorTheme theme, IImageResolver? imageResolver = null, Action? requestLayoutRefresh = null)
    {
        _theme = theme;
        _imageResolver = imageResolver;
        _requestLayoutRefresh = requestLayoutRefresh;
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
        var placeholder = new Run(string.IsNullOrEmpty(img.Alt) ? string.Empty : $"[{img.Alt}]")
        {
            Foreground = new SolidColorBrush(_theme.LinkColor),
        };

        if (_imageResolver is not null)
        {
            var resolver = _imageResolver;
            var requestLayoutRefresh = _requestLayoutRefresh;
            _ = placeholder.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => _ = LoadInlineImageAsync(placeholder, img, resolver, requestLayoutRefresh)));
        }

        return placeholder;
    }

    private static async Task LoadInlineImageAsync(
        Run placeholder,
        ImageInline img,
        IImageResolver resolver,
        Action? requestLayoutRefresh)
    {
        try
        {
            var imageData = await Task.Run(() => resolver.ResolveImageAsync(img.Url, CancellationToken.None)).ConfigureAwait(false);
            if (imageData is null)
            {
                await ReplaceWithBrokenImageAsync(placeholder, img.Url, requestLayoutRefresh).ConfigureAwait(false);
                return;
            }

            if (ImageElementFactory.IsSvg(imageData))
            {
                await placeholder.Dispatcher.InvokeAsync(() =>
                {
                    var svg = ImageElementFactory.CreateSvgBrowser(imageData, img.Alt, 300);
                    if (svg is not null)
                    {
                        svg.InvalidateMeasure();
                        svg.InvalidateArrange();
                        ReplaceInline(placeholder, new InlineUIContainer(svg) { BaselineAlignment = BaselineAlignment.Bottom });
                        requestLayoutRefresh?.Invoke();
                    }
                    else
                    {
                        ReplaceInline(placeholder, CreateBrokenInline(img.Url));
                        requestLayoutRefresh?.Invoke();
                    }
                });
                return;
            }

            var bitmap = ImageElementFactory.CreateBitmap(imageData);
            if (bitmap is null)
            {
                await ReplaceWithBrokenImageAsync(placeholder, img.Url, requestLayoutRefresh).ConfigureAwait(false);
                return;
            }

            await placeholder.Dispatcher.InvokeAsync(() =>
            {
                var imageControl = ImageElementFactory.CreateBitmapImageControl(bitmap, img.Alt, 300, alignLeft: false);
                imageControl.InvalidateMeasure();
                imageControl.InvalidateArrange();
                ReplaceInline(placeholder, new InlineUIContainer(imageControl) { BaselineAlignment = BaselineAlignment.Bottom });
                requestLayoutRefresh?.Invoke();
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            await ReplaceWithBrokenImageAsync(placeholder, img.Url, requestLayoutRefresh).ConfigureAwait(false);
        }
    }

    private static async Task ReplaceWithBrokenImageAsync(Run placeholder, string url, Action? requestLayoutRefresh)
    {
        await placeholder.Dispatcher.InvokeAsync(() =>
        {
            ReplaceInline(placeholder, CreateBrokenInline(url));
            requestLayoutRefresh?.Invoke();
        });
    }

    private static InlineUIContainer CreateBrokenInline(string url) =>
        new(ImageElementFactory.CreateBrokenImageIcon(url)) { BaselineAlignment = BaselineAlignment.Bottom };

    private static void ReplaceInline(Run placeholder, InlineUIContainer replacement)
    {
        switch (placeholder.Parent)
        {
            case Paragraph paragraph:
                ReplaceInline(paragraph.Inlines, placeholder, replacement);
                break;
            case Span span:
                ReplaceInline(span.Inlines, placeholder, replacement);
                break;
        }
    }

    private static void ReplaceInline(InlineCollection inlines, Run placeholder, InlineUIContainer replacement)
    {
        var next = placeholder.NextInline;
        inlines.Remove(placeholder);

        if (next is not null)
            inlines.InsertBefore(next, replacement);
        else
            inlines.Add(replacement);
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
