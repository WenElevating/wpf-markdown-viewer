using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownEditorRenderingTests
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
    public void MarkdownChange_RefreshesPreviewDocumentBindingWhenDocumentIsReused()
    {
        RunOnSta(() =>
        {
            using var editor = new MarkdownEditor
            {
                Markdown =
                    """
                    before

                    ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAACklEQVR4nGMAAQAABQABDQotxAAAAABJRU5ErkJggg==)

                    after
                    """,
            };

            Assert.True(WaitUntil(() => editor.PreviewViewer.Document is not null));
            var initialDocument = editor.PreviewViewer.Document;
            Assert.NotNull(initialDocument);

            var documentChangeCount = 0;
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                FlowDocumentScrollViewer.DocumentProperty,
                typeof(FlowDocumentScrollViewer));
            descriptor.AddValueChanged(editor.PreviewViewer, OnDocumentChanged);
            try
            {
                editor.Markdown =
                    """
                    changed before

                    ![](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAACklEQVR4nGMAAQAABQABDQotxAAAAABJRU5ErkJggg==)

                    changed after
                    """;

                Assert.True(WaitUntil(() => documentChangeCount >= 2));
                Assert.Same(initialDocument, editor.PreviewViewer.Document);
            }
            finally
            {
                descriptor.RemoveValueChanged(editor.PreviewViewer, OnDocumentChanged);
            }

            void OnDocumentChanged(object? sender, EventArgs e) => documentChangeCount++;
        });
    }

    [Fact]
    public void LoadFile_UsesDocumentDirectoryForRelativeImages()
    {
        RunOnSta(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "WpfMarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
            var imageDirectory = Path.Combine(root, "images");
            var markdownPath = Path.Combine(root, "readme.md");
            Directory.CreateDirectory(imageDirectory);
            File.WriteAllText(markdownPath, "![logo](images/logo.png)");
            File.WriteAllBytes(Path.Combine(imageDirectory, "logo.png"), Png1x1);

            try
            {
                using var editor = new MarkdownEditor();

                editor.LoadFile(markdownPath);

                Assert.True(WaitUntil(() => DocumentContainsImage(editor.PreviewViewer.Document)));
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

    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;

            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(10);
        }

        return condition();
    }

    private static bool DocumentContainsImage(FlowDocument? document)
    {
        if (document is null)
            return false;

        return document.Blocks.Cast<Block>().Any(BlockContainsImage);
    }

    private static bool BlockContainsImage(Block block) =>
        block switch
        {
            BlockUIContainer container => container.Child is Image image && image.Source is not null ||
                                          container.Child is ContentControl host && host.Content is Image hostImage && hostImage.Source is not null,
            Section section => section.Blocks.Cast<Block>().Any(BlockContainsImage),
            Paragraph paragraph => paragraph.Inlines.Cast<Inline>().Any(InlineContainsImage),
            Table table => table.RowGroups
                .SelectMany(group => group.Rows)
                .SelectMany(row => row.Cells)
                .SelectMany(cell => cell.Blocks.Cast<Block>())
                .Any(BlockContainsImage),
            _ => false
        };

    private static bool InlineContainsImage(Inline inline) =>
        inline switch
        {
            InlineUIContainer container => container.Child is Image image && image.Source is not null,
            Span span => span.Inlines.Cast<Inline>().Any(InlineContainsImage),
            _ => false
        };
}
