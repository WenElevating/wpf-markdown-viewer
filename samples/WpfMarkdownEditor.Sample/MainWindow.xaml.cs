using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WpfMarkdownEditor.Sample.Controls;
using WpfMarkdownEditor.Sample.Helpers;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow : Window
{
    private readonly TabManager _tabManager = new();
    private bool _isDarkTheme;
    private bool _isSidePanelVisible = true;
    private readonly DispatcherTimer _cursorUpdateTimer;
    private WpfMarkdownEditor.Wpf.Controls.MarkdownEditor? _currentEditor;

    // Commands
    public static readonly RoutedCommand ToggleSidePanelCommand = new();
    public static readonly RoutedCommand SaveCommand = new();

    public MainWindow()
    {
        InitializeComponent();

        // Setup commands
        InputBindings.Add(new KeyBinding(ToggleSidePanelCommand, Key.B, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(SaveCommand, Key.S, ModifierKeys.Control));

        CommandBindings.Add(new CommandBinding(ToggleSidePanelCommand, OnToggleSidePanel));
        CommandBindings.Add(new CommandBinding(SaveCommand, OnSave));

        // Cursor position update timer (50ms throttle)
        _cursorUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _cursorUpdateTimer.Tick += OnCursorUpdateTick;

        // Wire TabBar
        TabBarControl.TabManager = _tabManager;
        _tabManager.ActiveTabChanged += OnActiveTabChanged;

        // Wire Toolbar
        Toolbar.ThemeChanged += OnThemeChanged;

        // Wire SidePanel
        SidePanel.FileSelected += OnFileSelected;

        // Create welcome tab
        _tabManager.NewTab("Welcome.md", Constants.WelcomeMarkdown);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            int backdropType = (int)Win32Interop.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
            Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            UpdateMicaDarkMode(false);
        }
        catch
        {
            // Win10 fallback: use opaque background, keep rounded corners
            RootBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
        }
    }

    #region Tab Management

    private void OnActiveTabChanged(object? sender, EventArgs e)
    {
        var tab = _tabManager.ActiveTab;
        if (tab == null)
        {
            _currentEditor = null;
            Toolbar.Editor = null;
            SidePanel.BindEditor(null);
            _cursorUpdateTimer.Stop();
            return;
        }

        // Ensure editor is in ContentPanel
        if (tab.Editor != null && !ContentPanel.Children.Contains(tab.Editor))
        {
            ContentPanel.Children.Add(tab.Editor);
        }

        // Show active editor, hide others
        foreach (var t in _tabManager.Tabs)
        {
            if (t.Editor != null)
            {
                t.Editor.Visibility = (t == tab) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Update bindings
        _currentEditor = tab.Editor;
        Toolbar.Editor = tab.Editor;
        Toolbar.UpdateEditorBinding();
        SidePanel.BindEditor(tab.Editor);

        // Track IsDirty changes
        tab.PropertyChanged -= OnTabPropertyChanged;
        tab.PropertyChanged += OnTabPropertyChanged;

        // Start cursor tracking
        if (tab.Editor != null)
        {
            _cursorUpdateTimer.Start();
            UpdateCursorPosition();
        }

        StatusText.Text = tab.FilePath != string.Empty ? tab.FilePath : tab.FileName;
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // TabBar will pick up IsDirty changes through data binding
    }

    #endregion

    #region Theme Management

    private void OnThemeChanged(bool isDark)
    {
        _isDarkTheme = isDark;
        SwapThemeDictionary(isDark);

        // Apply theme to all MarkdownEditor instances
        var theme = isDark ? EditorTheme.Dark : EditorTheme.Light;
        foreach (var tab in _tabManager.Tabs.Where(t => t.Editor != null))
        {
            tab.Editor!.ApplyTheme(theme);
        }

        // Update Mica dark mode
        UpdateMicaDarkMode(isDark);

        StatusText.Text = isDark ? "Dark theme" : "Light theme";
    }

    private void SwapThemeDictionary(bool isDark)
    {
        var resources = Application.Current.Resources.MergedDictionaries;

        // Find and remove current theme
        ResourceDictionary? currentTheme = null;
        foreach (var rd in resources)
        {
            if (rd.Source != null && (rd.Source.OriginalString.Contains("FluentTheme.xaml") ||
                                       rd.Source.OriginalString.Contains("FluentTheme.Dark.xaml")))
            {
                currentTheme = rd;
                break;
            }
        }

        if (currentTheme != null)
        {
            resources.Remove(currentTheme);
        }

        // Add the new theme
        var newThemeSource = isDark
            ? new Uri("pack://application:,,,/Resources/FluentTheme.Dark.xaml")
            : new Uri("pack://application:,,,/Resources/FluentTheme.xaml");

        resources.Insert(0, new ResourceDictionary { Source = newThemeSource });
    }

    private void UpdateMicaDarkMode(bool isDark)
    {
        try
        {
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            int darkMode = isDark ? 1 : 0;
            Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }
        catch { /* Not supported on Win10 */ }
    }

    #endregion

    #region Sidebar Collapse

    private void OnToggleSidePanel(object sender, ExecutedRoutedEventArgs e)
    {
        _isSidePanelVisible = !_isSidePanelVisible;
        GridLengthAnimation.AnimateColumnWidth(SidePanelColumn, _isSidePanelVisible ? 260 : 0);
        SidePanel.Visibility = _isSidePanelVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    #region File Operations

    private void OnFileSelected(string path)
    {
        _tabManager.OpenFile(path);
    }

    private async void OnSave(object sender, ExecutedRoutedEventArgs e)
    {
        var tab = _tabManager.ActiveTab;
        if (tab?.Editor == null) return;

        if (!string.IsNullOrEmpty(tab.FilePath))
        {
            await tab.Editor.SaveFileAsync(tab.FilePath);
            tab.IsDirty = false;
            tab.Editor.MarkAsSaved();
            StatusText.Text = $"Saved: {tab.FilePath}";
        }
        else
        {
            // Untitled file - show save dialog
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
                DefaultExt = ".md",
                FileName = tab.FileName
            };

            if (dialog.ShowDialog() == true)
            {
                await tab.Editor.SaveFileAsync(dialog.FileName);
                StatusText.Text = $"Saved: {dialog.FileName}";
            }
        }
    }

    #endregion

    #region Status Bar

    private void OnCursorUpdateTick(object? sender, EventArgs e)
    {
        UpdateCursorPosition();
    }

    private void UpdateCursorPosition()
    {
        if (_currentEditor == null) return;

        var (line, col) = _currentEditor.GetCursorPosition();
        CursorInfo.Text = $"Ln {line}, Col {col} · UTF-8 · Markdown";
    }

    #endregion

    #region Window Controls

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        // TODO: Check for unsaved tabs before closing
        SystemCommands.CloseWindow(this);
    }

    #endregion
}
