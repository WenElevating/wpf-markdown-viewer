using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Controls;
using WpfMarkdownEditor.Wpf.Dialogs;
using WpfMarkdownEditor.Wpf.Localization;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Translation;
using WpfMarkdownEditor.Wpf.Translation.Providers;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow : Window
{
    private const double SidebarWidth = 260;
    private const int SidebarAnimMs = 200;
    private const int RecentFileDisplayPathMaxLength = 56;

    private readonly LocalizationService _localizationService;
    private readonly LocalizationSettingsService _localizationSettingsService;
    private readonly RecentFilesService _recentFilesService;
    private readonly FolderWorkspaceService _folderWorkspaceService = new();
    private readonly FileOperationService _fileOperationService = new();
    private readonly HtmlExportService _htmlExportService = new();
    private readonly QuickOpenService _quickOpenService = new();
    private string _currentThemeName = "GitHub";
    private string _statusKey = "Status.Ready";
    private object[] _statusArgs = [];
    private bool _sidebarOpen;
    private TranslationService? _translationService;
    private TranslationSettingsService? _translationSettings;
    private CancellationTokenSource? _translationCts;
    private TranslationProgressOverlay? _progressOverlay;
    private bool _isTranslating;
    private TranslationLanguage _lastTargetLanguage;
    private WorkspaceTreeNode? _workspaceRoot;
    private string? _workspaceFolderPath;
    private Dictionary<string, WorkspaceTreeNode> _workspaceIndex = new(StringComparer.OrdinalIgnoreCase);
    private bool _selectingWorkspaceNode;
    private CancellationTokenSource? _folderScanCts;
    private CancellationTokenSource? _recentFilesLoadCts;
    private CancellationTokenSource? _recentFilesHoverCts;
    private CancellationTokenSource? _recentFilesCloseCts;
    private IReadOnlyList<RecentFileEntry> _recentFilesCache = [];
    private bool _recentFilesCacheLoaded;
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _loadingFile;

    public MainWindow(
        string? filePath = null,
        LocalizationService? localizationService = null,
        LocalizationSettingsService? localizationSettingsService = null)
    {
        _localizationService = localizationService ?? new LocalizationService();
        _localizationSettingsService = localizationSettingsService
            ?? new LocalizationSettingsService(GetTranslationSettingsDirectory());
        _recentFilesService = new RecentFilesService(_localizationSettingsService.SettingsDirectory);

        if (localizationService == null)
            _localizationService.SetLanguage(SupportedLanguage.English);

        InitializeComponent();
        WeakEventManager<LocalizationService, LanguageChangedEventArgs>.AddHandler(
            _localizationService,
            nameof(LocalizationService.LanguageChanged),
            OnLanguageChanged);

        Editor.SetLocalizer(_localizationService);
        BuildThemeList();
        BuildLanguageList();
        ApplyTheme("GitHub");
        SetStatus("Status.Ready");
        _ = RefreshRecentFilesCacheAsync();

        // Reposition search panel when editor layout changes
        Editor.TextBox.SizeChanged += (_, _) =>
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                UpdateSearchPanelPosition();
        };

        Editor.MarkdownChanged += OnMarkdownChanged;

        if (filePath != null && File.Exists(filePath))
        {
            _loadingFile = true;
            Editor.LoadFile(filePath);
            _loadingFile = false;
            _currentFilePath = filePath;
            _isDirty = false;
            _recentFilesService.AddOrRefreshFile(filePath);
            AddRecentFileToCache(filePath);
            SetStatus("Status.FileLoaded", filePath);
            _ = LoadCurrentFileDirectoryTreeAsync(filePath);
        }
        else
        {
            _currentFilePath = null;
            _isDirty = false;
        }
        UpdateTitle();
    }

    protected override void OnClosed(EventArgs e)
    {
        WeakEventManager<LocalizationService, LanguageChangedEventArgs>.RemoveHandler(
            _localizationService,
            nameof(LocalizationService.LanguageChanged),
            OnLanguageChanged);
        _folderScanCts?.Cancel();
        _folderScanCts?.Dispose();
        _recentFilesHoverCts?.Cancel();
        _recentFilesCloseCts?.Cancel();
        _recentFilesLoadCts?.Cancel();
        base.OnClosed(e);
    }

    #region File Operations

    private void OnNewFile(object sender, RoutedEventArgs e) => NewFile();

    private void OnOpenFile(object sender, RoutedEventArgs e) => OpenFile();

    private async void OnSaveFile(object sender, RoutedEventArgs e) => await SaveCurrentFileAsync();

    private void OnNewWindow(object sender, RoutedEventArgs e) => NewWindow();
    private void OnOpenFolder(object sender, RoutedEventArgs e) => OpenFolder();
    private void OnQuickOpen(object sender, RoutedEventArgs e) => QuickOpen();
    private void OnOpenRecentFile(object sender, RoutedEventArgs e) => OpenRecentFileMenu(e);
    private async void OnOpenRecentFileMouseEnter(object sender, MouseEventArgs e) => await OpenRecentFileMenuAfterHoverDelayAsync();
    private void OnOpenRecentFileMouseLeave(object sender, MouseEventArgs e)
    {
        CancelRecentFilesHover();
        ScheduleRecentFilesMenuClose();
    }

    private void OnRecentFilesMenuMouseEnter(object sender, MouseEventArgs e) => CancelRecentFilesMenuClose();
    private void OnRecentFilesMenuMouseLeave(object sender, MouseEventArgs e) => ScheduleRecentFilesMenuClose();
    private async void OnSaveFileAs(object sender, RoutedEventArgs e) => await SaveCurrentFileAsAsync();
    private void OnMoveFile(object sender, RoutedEventArgs e) => MoveCurrentFile();
    private void OnShowFileProperties(object sender, RoutedEventArgs e) => ShowCurrentFileProperties();
    private void OnOpenFileLocation(object sender, RoutedEventArgs e) => OpenCurrentFileLocation();
    private async void OnShowInSidebar(object sender, RoutedEventArgs e) => await ShowCurrentFileInSidebarAsync();
    private void OnDeleteFile(object sender, RoutedEventArgs e) => DeleteCurrentFile();
    private void OnImportFile(object sender, RoutedEventArgs e) => ImportFileIntoDocument();
    private void OnExportHtml(object sender, RoutedEventArgs e) => ExportCurrentDocumentAsHtml();
    private void OnPrintFile(object sender, RoutedEventArgs e) => PrintCurrentDocument();
    private void OnPreferences(object sender, RoutedEventArgs e) => OpenPreferences();
    private void OnCloseWindow(object sender, RoutedEventArgs e) => CloseCurrentWindow();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N: e.Handled = true; NewFile(); break;
                case Key.O: e.Handled = true; OpenFile(); break;
                case Key.S: e.Handled = true; _ = SaveCurrentFileAsync(); break;
                case Key.F: e.Handled = true; ShowSearchPanel(); break;
                case Key.P: e.Handled = true; QuickOpen(); break;
                case Key.W: e.Handled = true; CloseCurrentWindow(); break;
                case Key.OemComma: e.Handled = true; OpenPreferences(); break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key)
            {
                case Key.N: e.Handled = true; NewWindow(); break;
                case Key.S: e.Handled = true; _ = SaveCurrentFileAsAsync(); break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift) && e.Key == Key.P)
        {
            e.Handled = true;
            PrintCurrentDocument();
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isDirty)
        {
            var fileName = _currentFilePath != null
                ? System.IO.Path.GetFileName(_currentFilePath)
                : _localizationService.GetString("MainWindow.Untitled");
            var result = ShowSaveConfirmation(fileName);

            switch (result)
            {
                case SaveConfirmationResult.Save:
                    if (!SaveCurrentFileSync()) e.Cancel = true;
                    break;
                case SaveConfirmationResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
        base.OnClosing(e);
    }

    private void NewFile()
    {
        if (!ConfirmSaveIfDirty()) return;
        _loadingFile = true;
        Editor.Markdown = string.Empty;
        Editor.DocumentPath = null;
        _loadingFile = false;
        _currentFilePath = null;
        _isDirty = false;
        UpdateTitle();
        SetStatus("Status.NewFile");
    }

    private void NewWindow()
    {
        var window = new MainWindow(null, _localizationService, _localizationSettingsService);
        window.Show();
    }

    private void OpenFile()
    {
        if (!ConfirmSaveIfDirty()) return;

        var dialog = new OpenFileDialog
        {
            Filter = _localizationService.GetString("FileDialog.MarkdownFilter"),
            DefaultExt = ".md"
        };
        if (dialog.ShowDialog() != true) return;

        OpenFilePath(dialog.FileName, confirmIfDirty: false);
    }

    private bool OpenFilePath(string path, bool confirmIfDirty = true)
    {
        if (confirmIfDirty && !ConfirmSaveIfDirty()) return false;

        try
        {
            _loadingFile = true;
            Editor.LoadFile(path);
            _loadingFile = false;
            _currentFilePath = path;
            _isDirty = false;
            _recentFilesService.AddOrRefreshFile(path);
            AddRecentFileToCache(path);
            _ = LoadCurrentFileDirectoryTreeAsync(path);
            UpdateTitle();
            SetStatus("Status.FileLoaded", path);
            return true;
        }
        catch (Exception ex)
        {
            _loadingFile = false;
            if (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                _recentFilesService.RemoveFile(path);
                RemoveRecentFileFromCache(path);
            }

            MessageBox.Show(_localizationService.Format("Error.LoadFile", ex.Message), _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async Task<bool> SaveCurrentFileAsync()
    {
        string? targetPath = _currentFilePath;

        if (targetPath == null)
            return await SaveCurrentFileAsAsync();

        try
        {
            await Editor.SaveFileAsync(targetPath);
            _currentFilePath = targetPath;
            Editor.DocumentPath = targetPath;
            _isDirty = false;
            _recentFilesService.AddOrRefreshFile(targetPath);
            AddRecentFileToCache(targetPath);
            _ = LoadCurrentFileDirectoryTreeAsync(targetPath);
            UpdateTitle();
            SetStatus("Status.FileSaved", targetPath);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(_localizationService.Format("Error.SaveFile", ex.Message), _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async Task<bool> SaveCurrentFileAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = _localizationService.GetString("FileDialog.MarkdownFilter"),
            DefaultExt = ".md",
            FileName = _currentFilePath != null ? System.IO.Path.GetFileName(_currentFilePath) : string.Empty,
        };
        if (dialog.ShowDialog() != true) return false;

        try
        {
            await Editor.SaveFileAsync(dialog.FileName);
            _currentFilePath = dialog.FileName;
            Editor.DocumentPath = dialog.FileName;
            _isDirty = false;
            _recentFilesService.AddOrRefreshFile(dialog.FileName);
            AddRecentFileToCache(dialog.FileName);
            _ = LoadCurrentFileDirectoryTreeAsync(dialog.FileName);
            UpdateTitle();
            SetStatus("Status.FileSaved", dialog.FileName);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(_localizationService.Format("Error.SaveFile", ex.Message), _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async void OpenFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = _localizationService.GetString("MainWindow.OpenFolder")
        };
        if (dialog.ShowDialog() != true)
            return;

        if (await LoadWorkspaceFolderAsync(dialog.FolderName))
        {
            ShowFilesTab();
            OpenSidebar();
        }
    }

    private async Task<bool> LoadWorkspaceFolderAsync(string folderPath)
    {
        _folderScanCts?.Cancel();
        _folderScanCts?.Dispose();
        var scanCts = new CancellationTokenSource();
        _folderScanCts = scanCts;

        try
        {
            var result = await _folderWorkspaceService.ScanShallowAsync(folderPath, scanCts.Token);
            if (scanCts.IsCancellationRequested)
                return false;

            ApplyWorkspaceResult(folderPath, result);

            if (result.IsTruncated)
                SetStatus("Status.FolderScanTruncated", result.MarkdownFileCount);
            else
                SetStatus("Status.Ready");

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
        finally
        {
            if (ReferenceEquals(_folderScanCts, scanCts))
            {
                _folderScanCts = null;
                scanCts.Dispose();
            }
        }
    }

    private void ApplyWorkspaceResult(string folderPath, FolderWorkspaceResult result)
    {
        _workspaceFolderPath = System.IO.Path.GetFullPath(folderPath);
        _workspaceRoot = result.Root;
        _workspaceIndex = BuildWorkspaceIndex(result.Root);
        FilesTree.ItemsSource = result.Root.Children;
        UpdateFilesPanelState();
    }

    private async Task<WorkspaceTreeNode?> LoadCurrentFileDirectoryTreeAsync(string filePath)
    {
        var fullPath = System.IO.Path.GetFullPath(filePath);
        if (_workspaceIndex.TryGetValue(fullPath, out var existingNode))
        {
            SelectWorkspaceNode(existingNode);
            return existingNode;
        }

        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !await LoadWorkspaceFolderAsync(directory))
            return null;

        if (!_workspaceIndex.TryGetValue(fullPath, out var node))
            return null;

        SelectWorkspaceNode(node);
        return node;
    }

    private async Task LoadWorkspaceNodeChildrenAsync(WorkspaceTreeNode node)
    {
        if (!node.IsDirectory || node.ChildrenLoaded)
            return;

        var result = await _folderWorkspaceService.ScanShallowAsync(node.FullPath);
        node.Children.Clear();
        foreach (var child in result.Root.Children)
            node.Children.Add(child);

        node.ChildrenLoaded = true;
        MergeWorkspaceIndex(node);
        UpdateFilesPanelState();
    }

    private static Dictionary<string, WorkspaceTreeNode> BuildWorkspaceIndex(WorkspaceTreeNode root)
    {
        var index = new Dictionary<string, WorkspaceTreeNode>(StringComparer.OrdinalIgnoreCase);

        void Visit(WorkspaceTreeNode node)
        {
            index[node.FullPath] = node;
            foreach (var child in node.Children)
                Visit(child);
        }

        Visit(root);
        return index;
    }

    private void MergeWorkspaceIndex(WorkspaceTreeNode node)
    {
        void Visit(WorkspaceTreeNode current)
        {
            _workspaceIndex[current.FullPath] = current;
            foreach (var child in current.Children)
                Visit(child);
        }

        Visit(node);
    }

    private void QuickOpen()
    {
        var items = _quickOpenService.BuildItems(
            _recentFilesService.LoadFiles(removeMissingFiles: true),
            _workspaceRoot);
        ShowQuickOpenDialog(items);
    }

    private void OpenRecentFileMenu(RoutedEventArgs? e = null)
    {
        if (e is not null)
            e.Handled = true;

        CancelRecentFilesHover();
        CancelRecentFilesMenuClose();
        SetOpenRecentFileButtonActive(isActive: true);
        RecentFilesPopup.IsOpen = true;
        if (_recentFilesCacheLoaded)
            RenderRecentFilesMenu(_recentFilesCache);
        else
            ShowRecentFilesLoadingState();

        _ = RefreshRecentFilesCacheAsync(renderWhenOpen: true);
    }

    private async Task OpenRecentFileMenuAfterHoverDelayAsync()
    {
        CancelRecentFilesHover();
        var hoverCts = new CancellationTokenSource();
        _recentFilesHoverCts = hoverCts;

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
            if (ReferenceEquals(_recentFilesHoverCts, hoverCts))
                _recentFilesHoverCts = null;

            hoverCts.Dispose();
        }
    }

    private void CancelRecentFilesHover()
    {
        _recentFilesHoverCts?.Cancel();
    }

    private void ScheduleRecentFilesMenuClose()
    {
        _recentFilesCloseCts?.Cancel();
        var closeCts = new CancellationTokenSource();
        _recentFilesCloseCts = closeCts;
        _ = CloseRecentFilesMenuIfPointerLeftAsync(closeCts);
    }

    private void CancelRecentFilesMenuClose()
    {
        _recentFilesCloseCts?.Cancel();
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
            if (ReferenceEquals(_recentFilesCloseCts, closeCts))
                _recentFilesCloseCts = null;

            closeCts.Dispose();
        }
    }

    private async Task RefreshRecentFilesCacheAsync(bool renderWhenOpen = false)
    {
        _recentFilesLoadCts?.Cancel();
        var loadCts = new CancellationTokenSource();
        _recentFilesLoadCts = loadCts;

        try
        {
            var entries = await _recentFilesService.LoadFilesSnapshotAsync(loadCts.Token);
            if (loadCts.IsCancellationRequested)
                return;

            _recentFilesCache = entries;
            _recentFilesCacheLoaded = true;
            if (renderWhenOpen && RecentFilesPopup.IsOpen)
                RenderRecentFilesMenu(_recentFilesCache);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_recentFilesLoadCts, loadCts))
                _recentFilesLoadCts = null;

            loadCts.Dispose();
        }
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
                Content = FormatRecentFileDisplayPath(entry.Path),
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

    private void AddRecentFileToCache(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        _recentFilesCache = _recentFilesCache
            .Where(entry => !string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentFileEntry(fullPath, DateTime.UtcNow))
            .Take(20)
            .ToList();
    }

    private void RemoveRecentFileFromCache(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        _recentFilesCache = _recentFilesCache
            .Where(entry => !string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
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

    private static string FormatRecentFileDisplayPath(string path)
    {
        if (path.Length <= RecentFileDisplayPathMaxLength)
            return path;

        var root = System.IO.Path.GetPathRoot(path);
        var fileName = System.IO.Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fileName))
            return path;

        return $"{root}...{System.IO.Path.DirectorySeparatorChar}{fileName}";
    }

    private void OnClearRecentFiles(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _recentFilesLoadCts?.Cancel();
        _recentFilesService.ClearFiles();
        _recentFilesCache = [];
        _recentFilesCacheLoaded = true;
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

    private void ShowQuickOpenDialog(IReadOnlyList<QuickOpenItem> items)
    {
        var dialog = new QuickOpenDialog(items) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedItem is not null)
            OpenFilePath(dialog.SelectedItem.Path);

        Editor.Focus();
    }

    private void MoveCurrentFile()
    {
        if (_currentFilePath is null)
            return;

        if (_isDirty && !SaveCurrentFileSync())
            return;

        var dialog = new SaveFileDialog
        {
            FileName = System.IO.Path.GetFileName(_currentFilePath),
            Filter = _localizationService.GetString("FileDialog.MarkdownFilter"),
            DefaultExt = ".md"
        };
        if (dialog.ShowDialog() != true)
            return;

        var oldFullPath = System.IO.Path.GetFullPath(_currentFilePath);
        var newFullPath = System.IO.Path.GetFullPath(dialog.FileName);
        if (string.Equals(oldFullPath, newFullPath, StringComparison.OrdinalIgnoreCase))
            return;

        var overwrite = false;
        if (File.Exists(newFullPath))
        {
            overwrite = MessageBox.Show(
                _localizationService.GetString("MainWindow.OverwriteFilePrompt"),
                _localizationService.GetString("Common.Validation"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
            if (!overwrite)
                return;
        }

        try
        {
            var oldPath = _currentFilePath;
            _fileOperationService.MoveFile(oldPath, dialog.FileName, overwrite);
            _recentFilesService.RemoveFile(oldPath);
            _recentFilesService.AddOrRefreshFile(dialog.FileName);
            RemoveRecentFileFromCache(oldPath);
            AddRecentFileToCache(dialog.FileName);
            RemoveWorkspaceNode(oldPath);
            _currentFilePath = dialog.FileName;
            Editor.DocumentPath = dialog.FileName;
            _ = LoadCurrentFileDirectoryTreeAsync(dialog.FileName);
            UpdateTitle();
            SetStatus("Status.FileSaved", dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShowCurrentFileProperties()
    {
        if (_currentFilePath is null)
            return;

        try
        {
            var properties = _fileOperationService.GetProperties(_currentFilePath);
            MessageBox.Show(
                $"{properties.Path}{Environment.NewLine}{properties.SizeBytes:N0} bytes{Environment.NewLine}{properties.LastModifiedUtc:u}",
                _localizationService.GetString("MainWindow.Properties"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenCurrentFileLocation()
    {
        if (_currentFilePath is null)
            return;

        try
        {
            _fileOperationService.OpenFileLocation(_currentFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ShowCurrentFileInSidebarAsync()
    {
        if (_currentFilePath is null)
            return;

        var node = await LoadCurrentFileDirectoryTreeAsync(_currentFilePath);
        if (node is null)
            return;

        ShowFilesTab();
        OpenSidebar();
        BringWorkspaceNodeIntoView(node);
    }

    private void SelectWorkspaceNode(WorkspaceTreeNode node)
    {
        _selectingWorkspaceNode = true;
        try
        {
            foreach (var indexedNode in _workspaceIndex.Values)
                indexedNode.IsSelected = false;

            for (var current = node; current is not null; current = current.Parent)
                current.IsExpanded = true;

            node.IsSelected = true;
        }
        finally
        {
            _selectingWorkspaceNode = false;
        }
    }

    private void BringWorkspaceNodeIntoView(WorkspaceTreeNode node)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var item = FindTreeViewItem(FilesTree, node);
            item?.BringIntoView();
            item?.Focus();
        }, DispatcherPriority.Background);
    }

    private static TreeViewItem? FindTreeViewItem(ItemsControl container, WorkspaceTreeNode target)
    {
        container.UpdateLayout();

        foreach (var item in container.Items)
        {
            if (container.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeViewItem)
                continue;

            if (ReferenceEquals(item, target))
                return treeViewItem;

            var childItem = FindTreeViewItem(treeViewItem, target);
            if (childItem is not null)
                return childItem;
        }

        return null;
    }

    private void DeleteCurrentFile()
    {
        if (_currentFilePath is null)
            return;

        var confirm = MessageBox.Show(
            _localizationService.GetString("MainWindow.DeleteFilePrompt"),
            _localizationService.GetString("MainWindow.DeleteFile"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            var deletedPath = _currentFilePath;
            _fileOperationService.DeleteFile(deletedPath);
            _recentFilesService.RemoveFile(deletedPath);
            RemoveRecentFileFromCache(deletedPath);
            RemoveWorkspaceNode(deletedPath);
            _loadingFile = true;
            Editor.Markdown = string.Empty;
            Editor.DocumentPath = null;
            _loadingFile = false;
            _currentFilePath = null;
            _isDirty = false;
            UpdateTitle();
            SetStatus("Status.NewFile");
        }
        catch (Exception ex)
        {
            _loadingFile = false;
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportFileIntoDocument()
    {
        var dialog = new OpenFileDialog
        {
            Filter = _localizationService.GetString("FileDialog.ImportFilter"),
            DefaultExt = ".md"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            Editor.AppendMarkdown(File.ReadAllText(dialog.FileName));
            _isDirty = true;
            UpdateTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportCurrentDocumentAsHtml()
    {
        var dialog = new SaveFileDialog
        {
            Filter = _localizationService.GetString("FileDialog.HtmlFilter"),
            DefaultExt = ".html"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var title = _currentFilePath is null
                ? _localizationService.GetString("MainWindow.Untitled")
                : System.IO.Path.GetFileNameWithoutExtension(_currentFilePath);
            _htmlExportService.ExportHtmlToFile(Editor.Markdown, title, dialog.FileName);
            SetStatus("Status.FileSaved", dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                _localizationService.Format("Error.FileOperation", ex.Message),
                _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void PrintCurrentDocument()
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;

        var document = Editor.TryGetPrintablePreviewDocument(out var previewDocument)
            ? previewDocument
            : Editor.CreatePlainTextPrintDocument();
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, Title);
    }

    private void OpenPreferences()
    {
        var dialog = new PreferencesDialog(_localizationService, _localizationService.CurrentLanguage, _currentThemeName)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
            return;

        _localizationService.SetLanguage(dialog.SelectedLanguage);
        _localizationSettingsService.SaveLanguage(dialog.SelectedLanguage);
        ApplyTheme(dialog.SelectedThemeName);

        if (dialog.OpenTranslationSettingsRequested)
            OpenTranslationSettings();
    }

    private void OpenTranslationSettings()
    {
        var settings = GetTranslationSettings();
        var activeProvider = settings.GetActiveProvider() ?? "Baidu";
        var existingConfig = settings.LoadConfig(activeProvider);
        var dialog = new TranslationConfigDialog(
            isFirstRun: false,
            preselectedProvider: activeProvider,
            existingConfig: existingConfig,
            localizer: _localizationService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            settings.SaveConfig(dialog.SavedConfig!);
            settings.SetActiveProvider(dialog.SavedConfig!.ProviderName);
        }
    }

    private void CloseCurrentWindow() => Close();

    private void RemoveWorkspaceNode(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!_workspaceIndex.Remove(fullPath, out var node))
            return;

        node.Parent?.Children.Remove(node);
    }

    private bool SaveCurrentFileSync()
    {
        string? targetPath = _currentFilePath;

        if (targetPath == null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = _localizationService.GetString("FileDialog.MarkdownFilter"),
                DefaultExt = ".md"
            };
            if (dialog.ShowDialog() != true) return false;
            targetPath = dialog.FileName;
        }

        try
        {
            File.WriteAllText(targetPath, Editor.Markdown);
            _currentFilePath = targetPath;
            Editor.DocumentPath = targetPath;
            _isDirty = false;
            _recentFilesService.AddOrRefreshFile(targetPath);
            AddRecentFileToCache(targetPath);
            _ = LoadCurrentFileDirectoryTreeAsync(targetPath);
            UpdateTitle();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(_localizationService.Format("Error.SaveFile", ex.Message), _localizationService.GetString("Common.Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool ConfirmSaveIfDirty()
    {
        if (!_isDirty) return true;

        var fileName = _currentFilePath != null
            ? System.IO.Path.GetFileName(_currentFilePath)
            : _localizationService.GetString("MainWindow.Untitled");
        var result = ShowSaveConfirmation(fileName);

        return result switch
        {
            SaveConfirmationResult.Save => SaveCurrentFileSync(),
            SaveConfirmationResult.DontSave => true,
            _ => false
        };
    }

    private SaveConfirmationResult ShowSaveConfirmation(string fileName)
    {
        var dialog = new SaveConfirmationDialog(fileName, _localizationService) { Owner = this };
        dialog.ShowDialog();
        return dialog.Result;
    }

    private void UpdateTitle()
    {
        var fileName = _currentFilePath != null
            ? System.IO.Path.GetFileName(_currentFilePath)
            : _localizationService.GetString("MainWindow.Untitled");
        Title = $"{fileName}{(_isDirty ? " *" : "")} - {_localizationService.GetString("MainWindow.TitleSuffix")}";
        UpdateFileScopedMenuItems();
    }

    private void UpdateFileScopedMenuItems()
    {
        var hasCurrentFile = _currentFilePath is not null;
        FilePropertiesButton.IsEnabled = hasCurrentFile;
        OpenFileLocationButton.IsEnabled = hasCurrentFile;
        ShowInSidebarButton.IsEnabled = hasCurrentFile;
        DeleteFileButton.IsEnabled = hasCurrentFile;
    }

    #endregion

    #region Theme

    private void BuildThemeList()
    {
        ThemeListPanel.Children.Clear();
        foreach (var theme in ThemeCatalog.Themes)
        {
            var item = new RadioButton
            {
                GroupName = "AppThemes",
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Ellipse
                        {
                            Width = 14, Height = 14,
                            Fill = new SolidColorBrush(theme.DotColor),
                            Stroke = new SolidColorBrush(theme.DotBorder),
                            StrokeThickness = 1,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                        new TextBlock
                        {
                            Text = theme.Name,
                            Margin = new Thickness(10, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    }
                },
                Tag = theme.Name,
                Style = (Style)FindResource("ThemeItemStyle"),
            };
            item.Checked += OnThemeItemSelected;
            ThemeListPanel.Children.Add(item);
        }
    }

    private void OnThemeItemSelected(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string name) return;
        ApplyTheme(name);
        ViewPopup.IsOpen = false;
    }

    private void ApplyTheme(string name)
    {
        var entry = ThemeCatalog.Themes.FirstOrDefault(t => t.Name == name);
        if (entry == null) return;

        _currentThemeName = name;
        ApplyWindowTheme(entry.IsDark);
        Editor.ApplyTheme(entry.Theme);

        // Update radio selection
        foreach (var child in ThemeListPanel.Children)
        {
            if (child is RadioButton rb)
                rb.IsChecked = (rb.Tag as string) == name;
        }

        SetStatus("Status.ThemeApplied", name);
    }

    private void ApplyWindowTheme(bool dark)
    {
        var r = Application.Current.Resources;
        SetBrushColor(r, "WindowBackgroundBrush", dark ? "#1E1E1E" : "#F3F3F3");
        SetBrushColor(r, "SurfaceBackgroundBrush", dark ? "#282828" : "#FAFAFA");
        SetBrushColor(r, "CardBackgroundBrush", dark ? "#2D2D2D" : "#FDFDFE");
        SetBrushColor(r, "TextPrimaryBrush", dark ? "#F3F6F8" : "#15171A");
        SetBrushColor(r, "TextSecondaryBrush", dark ? "#B5BBC4" : "#575D66");
        SetBrushColor(r, "TextDisabledBrush", dark ? "#6F7680" : "#838A96");
        SetBrushColor(r, "AccentBrush", dark ? "#60CDFF" : "#005FB8");
        SetBrushColor(r, "DividerBrush", dark ? "#3D3D3D" : "#DDE5F0");
        SetBrushColor(r, "HoverBackgroundBrush", dark ? "#383838" : "#F2F6FB");
        SetBrushColor(r, "PressedBackgroundBrush", dark ? "#434343" : "#E6EEF8");
        SetBrushColor(r, "SegmentBackgroundBrush", dark ? "#404040" : "#E0E0E0");
        SetBrushColor(r, "MenuBorderBrush", dark ? "#4A4A4A" : "#D8E1EC");
    }

    private static void SetBrushColor(ResourceDictionary r, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        r[key] = new SolidColorBrush(color);
    }

    private void BuildLanguageList()
    {
        LanguageListPanel.Children.Clear();
        foreach (var language in SupportedLanguage.All)
        {
            var item = new RadioButton
            {
                Content = _localizationService.GetString(language.DisplayKey),
                Tag = language,
                Style = (Style)FindResource("ThemeItemStyle"),
                IsChecked = language.Equals(_localizationService.CurrentLanguage),
            };
            item.Checked += OnLanguageItemSelected;
            LanguageListPanel.Children.Add(item);
        }
    }

    private void OnLanguageItemSelected(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not SupportedLanguage language)
            return;

        _localizationService.SetLanguage(language);
        _localizationSettingsService.SaveLanguage(language);
        ViewPopup.IsOpen = false;
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        BuildLanguageList();
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        UpdateTitle();
        RefreshStatusText();
        if (OutlinePanel.Visibility == Visibility.Visible)
            UpdateOutline();
        UpdateSearchCount();
    }

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
        StatusText.Text = _statusArgs.Length == 0
            ? _localizationService.GetString(_statusKey)
            : _localizationService.Format(_statusKey, _statusArgs);
    }

    private string GetTranslationLanguageDisplayName(TranslationLanguage language) => language switch
    {
        TranslationLanguage.English => _localizationService.GetString("Language.English"),
        TranslationLanguage.Chinese => _localizationService.GetString("Language.Chinese"),
        TranslationLanguage.Japanese => _localizationService.GetString("Translation.Language.Japanese"),
        TranslationLanguage.Korean => _localizationService.GetString("Translation.Language.Korean"),
        _ => language.ToString()
    };

    #endregion

    #region Formatting Toolbar

    private void OnPopupItemClick(object sender, RoutedEventArgs e)
    {
        RecentFilesPopup.IsOpen = false;
        FilePopup.IsOpen = false;
        EditPopup.IsOpen = false;
        ParagraphPopup.IsOpen = false;
        FormatPopup.IsOpen = false;
        InsertPopup.IsOpen = false;
        ViewPopup.IsOpen = false;
        ToolsPopup.IsOpen = false;
    }

    private void OnHeading1(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("#");
    private void OnHeading2(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("##");
    private void OnHeading3(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("###");
    private void OnBold(object sender, RoutedEventArgs e) => Editor.WrapSelection("**", "**");
    private void OnItalic(object sender, RoutedEventArgs e) => Editor.WrapSelection("*", "*");
    private void OnStrikethrough(object sender, RoutedEventArgs e) => Editor.WrapSelection("~~", "~~");
    private void OnInlineCode(object sender, RoutedEventArgs e) => Editor.WrapSelection("`", "`");
    private void OnLink(object sender, RoutedEventArgs e) => Editor.WrapSelection("[", "](url)");
    private void OnQuote(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix(">");
    private void OnUnorderedList(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("-");
    private void OnOrderedList(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("1.");
    private void OnCodeBlock(object sender, RoutedEventArgs e) => Editor.WrapSelection("```\n", "\n```");
    private void OnTable(object sender, RoutedEventArgs e)
    {
        InsertPopup.IsOpen = false;
        var dialog = new TableInsertDialog(_localizationService) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.Result is (int rows, int cols))
        {
            Editor.InsertText(GenerateTable(rows, cols));
        }
    }

    private static string GenerateTable(int dataRows, int columns)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('\n');

        // Header
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Range(1, columns).Select(i => $"Column {i}")));
        sb.Append(" |\n");

        // Separator
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Repeat("--------", columns)));
        sb.Append(" |\n");

        // Data rows
        var cellIndex = 1;
        for (var r = 0; r < dataRows; r++)
        {
            sb.Append("| ");
            sb.Append(string.Join(" | ", Enumerable.Range(0, columns).Select(_ => $"Cell {cellIndex++}")));
            sb.Append(" |\n");
        }

        return sb.ToString();
    }
    private void OnHorizontalRule(object sender, RoutedEventArgs e) => Editor.InsertText("\n---\n");
    private void OnUndo(object sender, RoutedEventArgs e) => Editor.TextBox.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => Editor.TextBox.Redo();
    private void OnFind(object sender, RoutedEventArgs e) => ShowSearchPanel();

    private void OnToggleSidebarFromMenu(object sender, RoutedEventArgs e)
    {
        ViewPopup.IsOpen = false;
        _sidebarOpen = !_sidebarOpen;
        AnimateSidebar(_sidebarOpen ? SidebarWidth : 0);
    }

    #endregion

    #region Search

    private readonly List<int> _searchMatches = [];
    private int _currentMatchIndex = -1;

    private void ShowSearchPanel()
    {
        UpdateSearchPanelPosition();
        SearchPanel.Visibility = Visibility.Visible;
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    private void UpdateSearchPanelPosition()
    {
        // Position over the editor TextBox column, not the preview column
        var previewAndSplitter = Editor.ActualWidth - Editor.TextBox.ActualWidth;
        SearchPanel.Margin = new Thickness(0, 4, previewAndSplitter + 8, 0);
    }

    private void HideSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        SearchInput.Text = "";
        _searchMatches.Clear();
        _currentMatchIndex = -1;
        SearchCount.Text = "";
    }

    private void OnSearchInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPanel.Visibility != Visibility.Visible) return;
        PerformSearch();
    }

    private void OnSearchInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var direction = Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1;
            // Defer navigation so Enter key isn't routed to the editor
            Dispatcher.BeginInvoke(() => NavigateSearch(direction));
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HideSearchPanel();
        }
    }

    private void OnSearchNext(object sender, RoutedEventArgs e) => NavigateSearch(1);
    private void OnSearchPrevious(object sender, RoutedEventArgs e) => NavigateSearch(-1);
    private void OnSearchClose(object sender, RoutedEventArgs e) => HideSearchPanel();

    private void PerformSearch()
    {
        _searchMatches.Clear();
        _currentMatchIndex = -1;

        var searchText = SearchInput.Text;
        if (string.IsNullOrEmpty(searchText))
        {
            SearchCount.Text = "";
            return;
        }

        var content = Editor.TextBox.Text;
        var pos = 0;
        while ((pos = content.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            _searchMatches.Add(pos);
            pos += searchText.Length;
        }

        if (_searchMatches.Count > 0)
        {
            _currentMatchIndex = 0;
            // Select without taking focus (keeps search input active)
            Editor.TextBox.Select(_searchMatches[0], searchText.Length);
        }

        UpdateSearchCount();
    }

    private void NavigateSearch(int direction)
    {
        if (_searchMatches.Count == 0) { PerformSearch(); return; }
        _currentMatchIndex = (_currentMatchIndex + direction + _searchMatches.Count) % _searchMatches.Count;
        GoToMatch(_currentMatchIndex);
        UpdateSearchCount();
    }

    private void GoToMatch(int index)
    {
        var textBox = Editor.TextBox;
        var pos = _searchMatches[index];
        var length = SearchInput.Text.Length;
        textBox.Select(pos, length);
        // Briefly focus editor to trigger auto-scroll, then return to search
        textBox.Focus();
        Dispatcher.BeginInvoke(() => SearchInput.Focus());
    }

    private void UpdateSearchCount()
    {
        SearchCount.Text = _searchMatches.Count > 0
            ? $"{_currentMatchIndex + 1}/{_searchMatches.Count}"
            : _localizationService.GetString("MainWindow.NoResults");
    }

    #endregion

    #region Sidebar

    private void OnToggleSidebar(object sender, RoutedEventArgs e)
    {
        _sidebarOpen = !_sidebarOpen;
        AnimateSidebar(_sidebarOpen ? SidebarWidth : 0);
    }

    private void AnimateSidebar(double targetWidth)
    {
        var isOpening = targetWidth > 0;
        var currentOffset = SidebarTranslateTransform.X;
        SidebarTranslateTransform.BeginAnimation(TranslateTransform.XProperty, null);
        SidebarColumn.Width = new GridLength(isOpening ? SidebarWidth : 0);

        var anim = new DoubleAnimation
        {
            From = currentOffset,
            To = isOpening ? 0 : -SidebarWidth,
            Duration = TimeSpan.FromMilliseconds(SidebarAnimMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };

        anim.Completed += (_, _) =>
        {
            SidebarTranslateTransform.X = isOpening ? 0 : -SidebarWidth;
            SidebarColumn.Width = new GridLength(isOpening ? SidebarWidth : 0);
        };

        SidebarTranslateTransform.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void OpenSidebar()
    {
        if (_sidebarOpen)
            return;

        _sidebarOpen = true;
        AnimateSidebar(SidebarWidth);
    }

    private void OnTabOutline(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        TabFiles.FontWeight = FontWeights.Normal;
        TabFiles.Foreground = (Brush)FindResource("TextSecondaryBrush");
        TabOutline.FontWeight = FontWeights.SemiBold;
        TabOutline.Foreground = (Brush)FindResource("TextPrimaryBrush");
        FilesUnderline.Visibility = Visibility.Collapsed;
        OutlineUnderline.Visibility = Visibility.Visible;

        FilesPanel.Visibility = Visibility.Collapsed;
        OutlinePanel.Visibility = Visibility.Visible;
        UpdateOutline();
    }

    private void OnTabFiles(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowFilesTab();
    }

    private void ShowFilesTab()
    {
        TabFiles.FontWeight = FontWeights.SemiBold;
        TabFiles.Foreground = (Brush)FindResource("TextPrimaryBrush");
        TabOutline.FontWeight = FontWeights.Normal;
        TabOutline.Foreground = (Brush)FindResource("TextSecondaryBrush");
        FilesUnderline.Visibility = Visibility.Visible;
        OutlineUnderline.Visibility = Visibility.Collapsed;

        FilesPanel.Visibility = Visibility.Visible;
        OutlinePanel.Visibility = Visibility.Collapsed;
        UpdateFilesPanelState();
    }

    private void UpdateFilesPanelState()
    {
        var hasWorkspaceFiles = _workspaceRoot?.Children.Count > 0 == true;
        FilesTree.Visibility = hasWorkspaceFiles ? Visibility.Visible : Visibility.Collapsed;
        FilesEmptyPanel.Visibility = hasWorkspaceFiles ? Visibility.Collapsed : Visibility.Visible;

        if (_workspaceRoot is null)
        {
            FilesEmptyTitle.Text = _localizationService.GetString("MainWindow.NoFolderOpened");
            FilesEmptyHint.Text = _localizationService.GetString("MainWindow.OpenFolderHint");
            return;
        }

        FilesEmptyTitle.Text = _localizationService.GetString("MainWindow.NoMarkdownFiles");
        FilesEmptyHint.Text = _localizationService.GetString("MainWindow.NoMarkdownFilesHint");
    }

    private void OnFilesTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_selectingWorkspaceNode)
            return;

        if (e.NewValue is WorkspaceTreeNode { IsDirectory: false } node)
            OpenFilePath(node.FullPath);
    }

    private async void OnFilesTreeItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: WorkspaceTreeNode node })
            await LoadWorkspaceNodeChildrenAsync(node);
    }

    private void OnMarkdownChanged(object? sender, EventArgs e)
    {
        if (!_loadingFile)
        {
            _isDirty = true;
            UpdateTitle();
        }
        if (OutlinePanel.Visibility == Visibility.Visible)
            UpdateOutline();
    }

    private void UpdateOutline()
    {
        OutlineList.Children.Clear();

        var markdown = Editor.Markdown;
        if (string.IsNullOrEmpty(markdown))
        {
            OutlineList.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("MainWindow.NoHeadingsFound"),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 32, 0, 0),
            });
            return;
        }

        var headingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
        var matches = headingRegex.Matches(markdown);

        if (matches.Count == 0)
        {
            OutlineList.Children.Add(new TextBlock
            {
                Text = _localizationService.GetString("MainWindow.NoHeadingsFound"),
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 32, 0, 0),
            });
            return;
        }

        foreach (Match match in matches)
        {
            var level = match.Groups[1].Value.Length;
            var title = match.Groups[2].Value.Trim();
            var indent = (level - 1) * 12;
            var fontSize = Math.Max(11, 14 - level);

            var btn = new Button
            {
                Style = (Style)FindResource("SidebarItemStyle"),
                Content = new TextBlock
                {
                    Text = title,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                    FontSize = fontSize,
                    FontWeight = level <= 2 ? FontWeights.SemiBold : FontWeights.Normal,
                },
                Margin = new Thickness(indent, 0, 0, 0),
            };
            OutlineList.Children.Add(btn);
        }
    }

    #endregion

    #region Translation

    private TranslationSettingsService GetTranslationSettings()
        => _translationSettings ??= new TranslationSettingsService(GetTranslationSettingsDirectory());

    private static string GetTranslationSettingsDirectory()
        => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WpfMarkdownEditor.Sample");

    private void OnTranslatePopupOpened(object? sender, EventArgs e)
    {
        // First-run: if no provider is configured, auto-trigger config dialog
        var settings = GetTranslationSettings();
        var activeProvider = settings.GetActiveProvider();
        if (activeProvider == null || settings.LoadConfig(activeProvider)?.IsComplete != true)
        {
            ToolsPopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(isFirstRun: true, localizer: _localizationService);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                settings.SaveConfig(dialog.SavedConfig!);
                activeProvider = dialog.SavedConfig!.ProviderName;
                settings.SetActiveProvider(activeProvider);
                EngineBaiduRadio.IsChecked = activeProvider == "Baidu";
                EngineOpenAIRadio.IsChecked = activeProvider == "OpenAI";
            }
        }
    }

    private void OnEngineRadioChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return; // Ignore events during XAML initialization

        var settings = GetTranslationSettings();
        var newEngine = EngineBaiduRadio.IsChecked == true ? "Baidu" : "OpenAI";
        var config = settings.LoadConfig(newEngine);

        if (config?.IsComplete != true)
        {
            ToolsPopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(
                isFirstRun: false,
                preselectedProvider: newEngine,
                existingConfig: config,
                localizer: _localizationService);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                settings.SaveConfig(dialog.SavedConfig!);
                settings.SetActiveProvider(dialog.SavedConfig!.ProviderName);
            }
        }
        else
        {
            settings.SetActiveProvider(newEngine);
        }
    }

    private void OnTranslationSettings(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        OpenTranslationSettings();
    }

    private async Task TranslateDocumentAsync(TranslationLanguage targetLanguage)
    {
        if (_isTranslating) return;
        var settings = GetTranslationSettings();
        _lastTargetLanguage = targetLanguage;

        var activeProvider = settings.GetActiveProvider();
        if (activeProvider == null || settings.LoadConfig(activeProvider)?.IsComplete != true)
        {
            ToolsPopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(
                isFirstRun: activeProvider == null,
                preselectedProvider: activeProvider,
                existingConfig: activeProvider != null ? settings.LoadConfig(activeProvider) : null,
                localizer: _localizationService);
            dialog.Owner = this;
            if (dialog.ShowDialog() != true) return;

            settings.SaveConfig(dialog.SavedConfig!);
            activeProvider = dialog.SavedConfig!.ProviderName;
            settings.SetActiveProvider(activeProvider);

            EngineBaiduRadio.IsChecked = activeProvider == "Baidu";
            EngineOpenAIRadio.IsChecked = activeProvider == "OpenAI";
        }

        var config = settings.LoadConfig(activeProvider)!;
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        ITranslationProvider provider = activeProvider == "Baidu"
            ? new BaiduTranslateProvider(config, httpClient)
            : new OpenAICompatibleProvider(config, httpClient);

        _translationService = new TranslationService(provider, localizer: _localizationService);
        _isTranslating = true;
        CancelTranslateBtn.Visibility = Visibility.Visible;
        TranslateLanguagePanel.Visibility = Visibility.Collapsed;
        ToolsPopup.IsOpen = false;

        _progressOverlay = new TranslationProgressOverlay(_localizationService);
        _progressOverlay.CancelRequested += OnOverlayCancel;
        _progressOverlay.RetryRequested += OnOverlayRetry;
        _progressOverlay.CloseRequested += OnOverlayClose;

        var editorRootGrid = (Grid)Editor.Content;
        Grid.SetColumnSpan(_progressOverlay, editorRootGrid.ColumnDefinitions.Count);
        editorRootGrid.Children.Add(_progressOverlay);
        _progressOverlay.Show();

        var progress = new Progress<TranslationProgress>(p => _progressOverlay.UpdateProgress(p));
        _translationCts = new CancellationTokenSource();

        try
        {
            var result = await _translationService.TranslateMarkdownAsync(
                Editor.Markdown, targetLanguage, progress, _translationCts.Token);

            Editor.RenderTranslatedPreview(result.TranslatedText);

            ClearTranslationBtn.Visibility = Visibility.Visible;
            SetStatus(
                "Status.TranslationPreview",
                GetTranslationLanguageDisplayName(result.DetectedSourceLanguage),
                GetTranslationLanguageDisplayName(targetLanguage));
            _progressOverlay.Hide();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Status.TranslationCancelled");
            _progressOverlay.Hide();
        }
        catch (TimeoutException ex)
        {
            _progressOverlay.ShowError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _progressOverlay.ShowError(_localizationService.Format("Error.Network", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _progressOverlay.ShowError(ex.Message);
        }
        finally
        {
            _isTranslating = false;
            CancelTranslateBtn.Visibility = Visibility.Collapsed;
            TranslateLanguagePanel.Visibility = Visibility.Visible;
            _translationCts?.Dispose();
            _translationCts = null;
            httpClient.Dispose();
        }
    }

    private void OnTranslateToEnglish(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.English);
    private void OnTranslateToChinese(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.Chinese);
    private void OnTranslateToJapanese(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.Japanese);
    private void OnTranslateToKorean(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.Korean);

    private void OnCancelTranslate(object sender, RoutedEventArgs e)
    {
        _translationCts?.Cancel();
        ToolsPopup.IsOpen = false;
    }

    private void OnClearTranslation(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        Editor.ClearTranslatedPreview();
        ClearTranslationBtn.Visibility = Visibility.Collapsed;
        SetStatus("Status.TranslationCleared");
    }

    private void OnOverlayCancel(object? sender, EventArgs e) => _translationCts?.Cancel();
    private void OnOverlayRetry(object? sender, EventArgs e) => _ = TranslateDocumentAsync(_lastTargetLanguage);
    private void OnOverlayClose(object? sender, EventArgs e)
    {
        _progressOverlay?.Hide();
        if (_progressOverlay?.Parent is Panel panel)
            panel.Children.Remove(_progressOverlay);
    }

    #endregion
}
