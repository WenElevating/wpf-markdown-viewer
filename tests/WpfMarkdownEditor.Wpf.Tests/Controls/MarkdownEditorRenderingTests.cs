using System.ComponentModel;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownEditorRenderingTests
{
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
}
