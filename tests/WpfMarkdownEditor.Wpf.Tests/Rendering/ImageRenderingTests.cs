using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Wpf.Rendering;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Theming;
using Xunit;
using WpfBlock = System.Windows.Documents.Block;

namespace WpfMarkdownEditor.Wpf.Tests.Rendering;

public sealed class ImageRenderingTests
{
    private static readonly byte[] Png1x1 =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    [Fact]
    public void Render_LocalImagePathWithSpaces_CreatesImageControl()
    {
        RunOnSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "WpfMarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
            var imageDirectory = Path.Combine(root, "folder with spaces");
            var imagePath = Path.Combine(imageDirectory, "pasted image.png");
            Directory.CreateDirectory(imageDirectory);
            File.WriteAllBytes(imagePath, Png1x1);

            try
            {
                var parser = new MarkdownParser();
                using var imageLoader = new ImageLoader();
                var renderer = new FlowDocumentRenderer(EditorTheme.Light, imageLoader);

                var document = renderer.Render(parser.Parse($"![pasted]({imagePath})"));
                var block = Assert.IsType<BlockUIContainer>(Assert.Single(document.Blocks.Cast<WpfBlock>()));
                var image = Assert.IsType<Image>(block.Child);

                Assert.NotNull(image.Source);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
