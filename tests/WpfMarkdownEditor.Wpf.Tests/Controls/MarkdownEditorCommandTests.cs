using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownEditorCommandTests
{
    [Fact]
    public void CopyPlainTextCanExecute_NoSelection_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "text";
            editor.TextBox.Select(0, 0);

            Assert.False(MarkdownEditorCommands.CopyPlainText.CanExecute(null, editor));
        });
    }

    [Fact]
    public void PastePlainTextCanExecute_ClipboardHasNoText_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            ClearClipboard();

            Assert.False(MarkdownEditorCommands.PastePlainText.CanExecute(null, editor));
        });
    }

    [Fact]
    public void PasteImageCanExecute_ClipboardHasNoImage_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            ClearClipboard();

            Assert.False(MarkdownEditorCommands.PasteImage.CanExecute(null, editor));
        });
    }

    [Fact]
    public void MoveLineUpCanExecute_CaretOnFirstLine_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "one\ntwo";
            editor.TextBox.CaretIndex = 1;

            Assert.False(MarkdownEditorCommands.MoveLineUp.CanExecute(null, editor));
        });
    }

    [Fact]
    public void MoveLineUpCanExecute_SelectedBlockStartsOnFirstLine_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "one\ntwo\nthree";
            editor.TextBox.Select(0, 7);

            Assert.False(MarkdownEditorCommands.MoveLineUp.CanExecute(null, editor));
        });
    }

    [Fact]
    public void MoveLineDownCanExecute_CaretOnLastLine_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "one\ntwo";
            editor.TextBox.CaretIndex = editor.TextBox.Text.Length;

            Assert.False(MarkdownEditorCommands.MoveLineDown.CanExecute(null, editor));
        });
    }

    [Fact]
    public void MoveLineDown_SelectedLinesMoveAsBlock()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "one\ntwo\nthree";
            editor.TextBox.CaretIndex = 4;

            MarkdownEditorCommands.MoveLineDown.Execute(null, editor);

            Assert.Equal("one\nthree\ntwo", editor.TextBox.Text);
            Assert.Equal(10, editor.TextBox.SelectionStart);
            Assert.Equal(3, editor.TextBox.SelectionLength);
        });
    }

    [Fact]
    public void DeleteSelectionOrCurrentLineCanExecute_EmptyDocument_Disabled()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = string.Empty;

            Assert.False(MarkdownEditorCommands.DeleteSelectionOrCurrentLine.CanExecute(null, editor));
        });
    }

    [Fact]
    public void DeleteSelectionOrCurrentLine_NoSelectionDeletesCurrentLine()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "one\ntwo\nthree";
            editor.TextBox.CaretIndex = 4;

            MarkdownEditorCommands.DeleteSelectionOrCurrentLine.Execute(null, editor);

            Assert.Equal("one\nthree", editor.TextBox.Text);
            Assert.Equal(4, editor.TextBox.CaretIndex);
        });
    }

    [Fact]
    public void InsertHardLineBreak_InsertsMarkdownHardBreak()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            editor.TextBox.Text = "one";
            editor.TextBox.CaretIndex = 3;

            MarkdownEditorCommands.InsertHardLineBreak.Execute(null, editor);

            Assert.Equal("one  " + Environment.NewLine, editor.TextBox.Text);
            Assert.Equal(editor.TextBox.Text.Length, editor.TextBox.CaretIndex);
        });
    }

    private static void ClearClipboard()
    {
        RetryClipboard(Clipboard.Clear);
    }

    private static void RetryClipboard(Action action)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (COMException) when (attempt < 4)
            {
                Thread.Sleep(50);
            }
        }
    }
}
