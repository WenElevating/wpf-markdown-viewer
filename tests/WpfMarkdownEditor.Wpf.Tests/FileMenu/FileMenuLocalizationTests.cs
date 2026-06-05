using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class FileMenuLocalizationTests
{
    [Theory]
    [InlineData("MainWindow.NewWindow")]
    [InlineData("MainWindow.OpenFolder")]
    [InlineData("MainWindow.QuickOpen")]
    [InlineData("MainWindow.OpenRecentFile")]
    [InlineData("MainWindow.NoRecentFiles")]
    [InlineData("MainWindow.LoadingRecentFiles")]
    [InlineData("MainWindow.ClearRecentFiles")]
    [InlineData("MainWindow.SaveAs")]
    [InlineData("MainWindow.MoveTo")]
    [InlineData("MainWindow.Properties")]
    [InlineData("MainWindow.OpenFileLocation")]
    [InlineData("MainWindow.ShowInSidebar")]
    [InlineData("MainWindow.DeleteFile")]
    [InlineData("MainWindow.Import")]
    [InlineData("MainWindow.ExportHtml")]
    [InlineData("MainWindow.Print")]
    [InlineData("MainWindow.Preferences")]
    [InlineData("MainWindow.CloseWindow")]
    [InlineData("MainWindow.PasteImage")]
    [InlineData("MainWindow.CopyPlainText")]
    [InlineData("MainWindow.PastePlainText")]
    [InlineData("MainWindow.MoveLineUp")]
    [InlineData("MainWindow.MoveLineDown")]
    [InlineData("MainWindow.Delete")]
    [InlineData("MainWindow.InsertHardLineBreak")]
    [InlineData("MainWindow.Underline")]
    [InlineData("MainWindow.Comment")]
    [InlineData("MainWindow.Hyperlink")]
    [InlineData("MainWindow.ClearStyle")]
    [InlineData("MainWindow.Heading4")]
    [InlineData("MainWindow.Heading5")]
    [InlineData("MainWindow.Heading6")]
    [InlineData("MainWindow.ParagraphStyle")]
    [InlineData("MainWindow.InsertParagraphAbove")]
    [InlineData("MainWindow.InsertParagraphBelow")]
    [InlineData("MainWindow.Files")]
    [InlineData("MainWindow.NoFolderOpened")]
    [InlineData("MainWindow.OpenFolderHint")]
    [InlineData("MainWindow.NoMarkdownFiles")]
    [InlineData("MainWindow.NoMarkdownFilesHint")]
    [InlineData("MainWindow.OverwriteFilePrompt")]
    [InlineData("MainWindow.DeleteFilePrompt")]
    [InlineData("Error.FileOperation")]
    [InlineData("Status.FolderScanTruncated")]
    [InlineData("FileDialog.ImportFilter")]
    [InlineData("FileDialog.HtmlFilter")]
    public void NewFileMenuKeys_ExistInEnglishAndChinese(string key)
    {
        Assert.True(LocalizationStrings.English.ContainsKey(key), key);
        Assert.True(LocalizationStrings.Chinese.ContainsKey(key), key);
    }
}
