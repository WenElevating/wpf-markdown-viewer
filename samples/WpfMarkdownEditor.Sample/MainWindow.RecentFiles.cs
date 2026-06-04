using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfMarkdownEditor.Sample.ViewModels;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow
{
    private void OpenRecentFileMenu(RoutedEventArgs? e = null)
    {
        if (e is not null)
            e.Handled = true;

        CancelRecentFilesHover();
        CancelRecentFilesMenuClose();
        SetOpenRecentFileButtonActive(isActive: true);
        RecentFilesPopup.IsOpen = true;
        if (_recentFilesMenuViewModel.IsCacheLoaded)
            RenderRecentFilesMenu(_recentFilesMenuViewModel.Entries);
        else
            ShowRecentFilesLoadingState();

        _ = RefreshRecentFilesCacheAsync(renderWhenOpen: true);
    }

    private async Task OpenRecentFileMenuAfterHoverDelayAsync()
    {
        CancelRecentFilesHover();
        var hoverCts = _recentFilesMenuViewModel.StartHoverDelay();

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(400), hoverCts.Token);
            if (!OpenRecentFileButton.IsMouseOver)
                return;

            OpenRecentFileMenu();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _recentFilesMenuViewModel.CompleteHoverDelay(hoverCts);
        }
    }

    private void CancelRecentFilesHover()
    {
        _recentFilesMenuViewModel.CancelHoverDelay();
    }

    private void ScheduleRecentFilesMenuClose()
    {
        var closeCts = _recentFilesMenuViewModel.StartCloseDelay();
        _ = CloseRecentFilesMenuIfPointerLeftAsync(closeCts);
    }

    private void CancelRecentFilesMenuClose()
    {
        _recentFilesMenuViewModel.CancelCloseDelay();
    }

    private async Task CloseRecentFilesMenuIfPointerLeftAsync(CancellationTokenSource closeCts)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(120), closeCts.Token);
            if (OpenRecentFileButton.IsMouseOver || RecentFilesList.IsMouseOver)
                return;

            RecentFilesPopup.IsOpen = false;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _recentFilesMenuViewModel.CompleteCloseDelay(closeCts);
        }
    }

    private async Task RefreshRecentFilesCacheAsync(bool renderWhenOpen = false)
    {
        await _recentFilesMenuViewModel.RefreshAsync();
        if (renderWhenOpen && RecentFilesPopup.IsOpen)
            RenderRecentFilesMenu(_recentFilesMenuViewModel.Entries);
    }

    private void ShowRecentFilesLoadingState()
    {
        RecentFileItemsPanel.Children.Clear();
        RecentFileItemsPanel.Children.Add(CreateRecentFilesMessage("MainWindow.LoadingRecentFiles"));
    }

    private void RenderRecentFilesMenu(IReadOnlyList<RecentFileEntry> entries)
    {
        RecentFileItemsPanel.Children.Clear();
        if (entries.Count == 0)
        {
            RecentFileItemsPanel.Children.Add(CreateRecentFilesMessage("MainWindow.NoRecentFiles"));
            return;
        }

        foreach (var entry in entries)
        {
            var button = new Button
            {
                Content = RecentFilesMenuViewModel.FormatDisplayPath(entry.Path),
                CommandParameter = entry.Path,
                Tag = string.Empty,
                ToolTip = entry.Path,
                Style = (Style)FindResource("MenuItemStyle"),
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            button.Click += OnRecentFileItemClick;
            RecentFileItemsPanel.Children.Add(button);
        }
    }

    private TextBlock CreateRecentFilesMessage(string key)
    {
        return new TextBlock
        {
            Text = _localizationService.GetString(key),
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            FontSize = 12,
            Margin = new Thickness(16, 8, 16, 8),
        };
    }

    private void OnRecentFileItemClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { CommandParameter: string path })
            OpenFilePath(path);

        SetOpenRecentFileButtonActive(isActive: false);
        RecentFilesPopup.IsOpen = false;
        FilePopup.IsOpen = false;
    }

    private void OnClearRecentFiles(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _recentFilesMenuViewModel.Clear();
        RenderRecentFilesMenu([]);
        SetOpenRecentFileButtonActive(isActive: false);
        RecentFilesPopup.IsOpen = false;
        FilePopup.IsOpen = false;
    }

    private void OnRecentFilesPopupClosed(object? sender, EventArgs e)
    {
        SetOpenRecentFileButtonActive(isActive: false);
    }

    private void OnFilePopupClosed(object? sender, EventArgs e)
    {
        RecentFilesPopup.IsOpen = false;
        SetOpenRecentFileButtonActive(isActive: false);
    }

    private void SetOpenRecentFileButtonActive(bool isActive)
    {
        if (isActive)
        {
            OpenRecentFileButton.Background = (Brush)FindResource("HoverBackgroundBrush");
            return;
        }

        OpenRecentFileButton.ClearValue(BackgroundProperty);
    }
}
