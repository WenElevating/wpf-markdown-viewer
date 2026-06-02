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
