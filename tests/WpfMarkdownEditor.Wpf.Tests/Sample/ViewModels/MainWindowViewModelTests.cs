using WpfMarkdownEditor.Sample.ViewModels;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void CurrentFilePath_SetToPath_UpdatesTitleAndFileScopedState()
    {
        var localization = new LocalizationService();
        var viewModel = new MainWindowViewModel(localization);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SetCurrentFile(@"C:\docs\readme.md");

        Assert.Equal(@"C:\docs\readme.md", viewModel.CurrentFilePath);
        Assert.False(viewModel.IsDirty);
        Assert.True(viewModel.HasCurrentFile);
        Assert.Equal("readme.md - Quillora", viewModel.Title);
        Assert.Contains(nameof(MainWindowViewModel.CurrentFilePath), changed);
        Assert.Contains(nameof(MainWindowViewModel.HasCurrentFile), changed);
        Assert.Contains(nameof(MainWindowViewModel.Title), changed);
    }

    [Fact]
    public void MarkDirty_WhenDocumentLoaded_AddsDirtyMarkerToTitle()
    {
        var localization = new LocalizationService();
        var viewModel = new MainWindowViewModel(localization);

        viewModel.SetCurrentFile(@"C:\docs\readme.md");
        viewModel.MarkDirty();

        Assert.True(viewModel.IsDirty);
        Assert.Equal("readme.md * - Quillora", viewModel.Title);
    }

    [Fact]
    public void SetStatus_StoresLocalizedStatusText()
    {
        var localization = new LocalizationService();
        var viewModel = new MainWindowViewModel(localization);

        viewModel.SetStatus("Status.FileLoaded", @"C:\docs\readme.md");

        Assert.Contains(@"C:\docs\readme.md", viewModel.StatusText);
    }
}
