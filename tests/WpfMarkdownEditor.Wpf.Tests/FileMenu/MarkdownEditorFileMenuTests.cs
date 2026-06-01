using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class MarkdownEditorFileMenuTests
{
    [Fact]
    public void AppendMarkdown_AppendsWithBlankLineAndMovesCaret()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var editor = new MarkdownEditor { Markdown = "# Existing" };

            editor.AppendMarkdown("Imported");

            Assert.Equal("# Existing\r\n\r\nImported", editor.Markdown);
            Assert.Equal(editor.Markdown.Length, editor.TextBox.CaretIndex);
        });
    }

    [Fact]
    public void CreatePlainTextPrintDocument_ContainsMarkdownText()
    {
        RunOnSta(() =>
        {
            EnsureApplication();
            var editor = new MarkdownEditor { Markdown = "# Existing" };

            var document = editor.CreatePlainTextPrintDocument();

            Assert.Contains("# Existing", new TextRange(document.ContentStart, document.ContentEnd).Text);
        });
    }

    private static void EnsureApplication()
    {
        if (Application.Current == null)
            _ = new Application();
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
