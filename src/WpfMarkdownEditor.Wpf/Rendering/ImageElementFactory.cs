using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using WpfMarkdownEditor.Core;

namespace WpfMarkdownEditor.Wpf.Rendering;

internal static class ImageElementFactory
{
    private static readonly Regex NumberRegex = new(@"[-+]?\d*\.?\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ContentControl CreateImageHost(FrameworkElement initialContent) =>
        new()
        {
            Content = initialContent,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Focusable = false,
        };

    public static void SetImageHostContent(ContentControl host, FrameworkElement content)
    {
        host.Content = content;
        host.InvalidateMeasure();
        host.InvalidateArrange();
        host.UpdateLayout();
    }

    public static bool IsSvg(ImageData imageData) =>
        string.Equals(imageData.Format, "svg", StringComparison.OrdinalIgnoreCase) ||
        Encoding.UTF8.GetString(imageData.Data.AsSpan(0, Math.Min(imageData.Data.Length, 256))).Contains("<svg", StringComparison.OrdinalIgnoreCase);

    public static BitmapImage? CreateBitmap(ImageData imageData)
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
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    public static Image CreateBitmapImageControl(BitmapImage bitmap, string? alt, double maxHeight, bool alignLeft)
    {
        var image = new ResponsiveImage(bitmap.PixelWidth, bitmap.PixelHeight, maxHeight)
        {
            Source = bitmap,
            MaxHeight = maxHeight,
            HorizontalAlignment = alignLeft ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
        };

        if (alt is not null)
            image.ToolTip = alt;

        return image;
    }

    public static WebBrowser? CreateSvgBrowser(ImageData imageData, string? alt, double maxHeight)
    {
        try
        {
            var size = ReadSvgSize(imageData.Data) ?? new Size(300, Math.Min(150, maxHeight));
            if (size.Height > maxHeight && maxHeight > 0)
            {
                var scale = maxHeight / size.Height;
                size = new Size(size.Width * scale, maxHeight);
            }

            var browser = new WebBrowser
            {
                Width = Math.Max(1, size.Width),
                Height = Math.Max(1, size.Height),
                HorizontalAlignment = HorizontalAlignment.Left,
                Focusable = false,
            };

            if (alt is not null)
                browser.ToolTip = alt;

            var base64 = Convert.ToBase64String(imageData.Data);
            browser.NavigateToString(
                $$"""
                <!doctype html>
                <html>
                <head>
                  <meta http-equiv="X-UA-Compatible" content="IE=edge" />
                  <style>
                    html, body { margin: 0; padding: 0; overflow: hidden; background: transparent; }
                    img { display: block; width: 100%; height: 100%; object-fit: contain; }
                  </style>
                </head>
                <body><img alt="" src="data:image/svg+xml;base64,{{base64}}" /></body>
                </html>
                """);

            return browser;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    public static FrameworkElement CreateBrokenImageIcon(string? tooltip)
    {
        var canvas = new Canvas
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = tooltip,
            SnapsToDevicePixels = true,
        };

        canvas.Children.Add(new Rectangle
        {
            Width = 13,
            Height = 13,
            Stroke = new SolidColorBrush(Color.FromRgb(0x8a, 0x8f, 0x98)),
            StrokeThickness = 1,
            Fill = Brushes.Transparent,
        });

        canvas.Children.Add(new Line
        {
            X1 = 2,
            Y1 = 11,
            X2 = 6,
            Y2 = 7,
            Stroke = new SolidColorBrush(Color.FromRgb(0x8a, 0x8f, 0x98)),
            StrokeThickness = 1,
        });

        canvas.Children.Add(new Line
        {
            X1 = 6,
            Y1 = 7,
            X2 = 12,
            Y2 = 12,
            Stroke = new SolidColorBrush(Color.FromRgb(0x8a, 0x8f, 0x98)),
            StrokeThickness = 1,
        });

        var dot = new Ellipse
        {
            Width = 2,
            Height = 2,
            Fill = new SolidColorBrush(Color.FromRgb(0x8a, 0x8f, 0x98)),
        };
        canvas.Children.Add(dot);
        Canvas.SetLeft(dot, 9);
        Canvas.SetTop(dot, 3);

        return canvas;
    }

    public static FrameworkElement CreateLoadingImagePlaceholder(string? tooltip)
    {
        var borderBrush = new SolidColorBrush(Color.FromRgb(0xc9, 0xd1, 0xd9));
        borderBrush.Freeze();

        return new Border
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = tooltip,
            Background = Brushes.Transparent,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            SnapsToDevicePixels = true,
        };
    }

    private static Size? ReadSvgSize(byte[] data)
    {
        try
        {
            var xml = Encoding.UTF8.GetString(data);
            var root = XDocument.Parse(xml).Root;
            if (root is null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                return null;

            var width = ParseSvgLength(root.Attribute("width")?.Value);
            var height = ParseSvgLength(root.Attribute("height")?.Value);
            if (width is > 0 && height is > 0)
                return new Size(width.Value, height.Value);

            var viewBox = root.Attribute("viewBox")?.Value;
            if (string.IsNullOrWhiteSpace(viewBox))
                return null;

            var values = NumberRegex.Matches(viewBox)
                .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                .ToArray();
            return values.Length == 4 && values[2] > 0 && values[3] > 0
                ? new Size(values[2], values[3])
                : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    private static double? ParseSvgLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = NumberRegex.Match(value);
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private sealed class ResponsiveImage(double naturalWidth, double naturalHeight, double maxHeight) : Image
    {
        protected override Size MeasureOverride(Size constraint)
        {
            var desired = ConstrainSize(naturalWidth, naturalHeight, ResolveAvailableWidth(constraint.Width), maxHeight);
            base.MeasureOverride(desired);
            return desired;
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var desired = ConstrainSize(naturalWidth, naturalHeight, ResolveAvailableWidth(arrangeSize.Width), maxHeight);
            return base.ArrangeOverride(desired);
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            Dispatcher.BeginInvoke(new Action(InvalidateMeasure), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private double ResolveAvailableWidth(double constraintWidth)
        {
            if (IsFinitePositive(constraintWidth))
                return constraintWidth;

            var viewer = FindAncestor<FlowDocumentScrollViewer>();
            if (viewer?.Document is null || !IsFinitePositive(viewer.ActualWidth))
                return double.PositiveInfinity;

            var padding = viewer.Document.PagePadding;
            return Math.Max(1, viewer.ActualWidth - padding.Left - padding.Right);
        }

        private static Size ConstrainSize(double width, double height, double maxWidth, double maxHeight)
        {
            if (width <= 0 || height <= 0)
                return new Size(double.NaN, double.NaN);

            var scale = 1d;
            if (IsFinitePositive(maxWidth) && width * scale > maxWidth)
                scale = maxWidth / width;

            if (IsFinitePositive(maxHeight) && height * scale > maxHeight)
                scale = Math.Min(scale, maxHeight / height);

            return new Size(Math.Max(1, width * scale), Math.Max(1, height * scale));
        }

        private T? FindAncestor<T>() where T : DependencyObject
        {
            DependencyObject? current = this;
            while (current is not null)
            {
                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
                if (current is T match)
                    return match;
            }

            return null;
        }

        private static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
