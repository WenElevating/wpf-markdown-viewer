using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Controls;
using WpfMarkdownEditor.Wpf.Dialogs;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Theming;
using WpfMarkdownEditor.Wpf.Translation;
using WpfMarkdownEditor.Wpf.Translation.Providers;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow : Window
{
    private record ThemeEntry(string Name, EditorTheme Theme, Color DotColor, Color DotBorder, bool IsDark);

    private static readonly ThemeEntry[] Themes =
    [
        new("GitHub", EditorTheme.GitHub, Color.FromRgb(0xfa, 0xfb, 0xfc), Color.FromRgb(0xd0, 0xd7, 0xde), false),
        new("GitHub Dark", EditorTheme.GitHubDark, Color.FromRgb(0x0d, 0x11, 0x17), Color.FromRgb(0x30, 0x36, 0x3d), true),
        new("Claude", EditorTheme.Claude, Color.FromRgb(0xfa, 0xf9, 0xf6), Color.FromRgb(0xd9, 0x77, 0x57), false),
        new("Claude Dark", EditorTheme.ClaudeDark, Color.FromRgb(0x1c, 0x1c, 0x1e), Color.FromRgb(0xd9, 0x77, 0x57), true),
        new("Light", EditorTheme.Light, Color.FromRgb(0xff, 0xff, 0xff), Color.FromRgb(0xdd, 0xdd, 0xdd), false),
        new("Dark", EditorTheme.Dark, Color.FromRgb(0x1e, 0x1e, 0x1e), Color.FromRgb(0x55, 0x55, 0x55), true),
    ];

    private const double SidebarWidth = 260;
    private const int SidebarAnimMs = 200;

    private string _currentThemeName = "GitHub";
    private bool _sidebarOpen;
    private readonly List<FileHistoryEntry> _fileHistory = [];
    private TranslationService? _translationService;
    private TranslationSettingsService? _translationSettings;
    private CancellationTokenSource? _translationCts;
    private TranslationProgressOverlay? _progressOverlay;
    private bool _isTranslating;
    private TranslationLanguage _lastTargetLanguage;

    private record FileHistoryEntry(string Path, DateTime OpenedAt);

    public MainWindow()
    {
        InitializeComponent();
        BuildThemeList();
        ApplyTheme("GitHub");

        Editor.MarkdownChanged += OnMarkdownChanged;

        Editor.Markdown = """
            # Welcome to WPF Markdown Editor

            ## Features

            - **Real-time preview** with less than 50ms latency
            - *Italic* and **bold** text
            - `Inline code` support
            - [Links](https://example.com)

            ## Code Block

            ```csharp
            public class HelloWorld
            {
                public static void Main()
                {
                    Console.WriteLine("Hello, Markdown!");
                }
            }
            ```

            ## Table

            | Feature | Status |
            | ------- | ------ |
            | Parser | Done |
            | Renderer | Done |
            | Theme | Done |

            ## Blockquote

            > This is a blockquote.
            > It supports **inline formatting**.

            ---

            *Built with .NET 8 and WPF. Zero external dependencies.*
            """;
    }

    #region File Operations

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                Editor.LoadFile(dialog.FileName);
                AddToHistory(dialog.FileName);
                StatusText.Text = $"Loaded: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OnSaveFile(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await Editor.SaveFileAsync(dialog.FileName);
                StatusText.Text = $"Saved: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Theme

    private void BuildThemeList()
    {
        foreach (var theme in Themes)
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
        ThemePopup.IsOpen = false;
    }

    private void ApplyTheme(string name)
    {
        var entry = Array.Find(Themes, t => t.Name == name);
        if (entry == null) return;

        _currentThemeName = name;
        CurrentThemeName.Text = name;
        ApplyWindowTheme(entry.IsDark);
        Editor.ApplyTheme(entry.Theme);

        // Update radio selection
        foreach (var child in ThemeListPanel.Children)
        {
            if (child is RadioButton rb)
                rb.IsChecked = (rb.Tag as string) == name;
        }

        StatusText.Text = $"{name} theme";
    }

    private void ApplyWindowTheme(bool dark)
    {
        var r = Application.Current.Resources;
        SetBrushColor(r, "WindowBackgroundBrush", dark ? "#1E1E1E" : "#FFFFFF");
        SetBrushColor(r, "SurfaceBackgroundBrush", dark ? "#282828" : "#FAFAFA");
        SetBrushColor(r, "CardBackgroundBrush", dark ? "#2D2D2D" : "#FFFFFF");
        SetBrushColor(r, "TextPrimaryBrush", dark ? "#FFFFFF" : "#1A1A1A");
        SetBrushColor(r, "TextSecondaryBrush", dark ? "#9E9E9E" : "#616161");
        SetBrushColor(r, "AccentBrush", dark ? "#60CDFF" : "#005FB8");
        SetBrushColor(r, "DividerBrush", dark ? "#3D3D3D" : "#E5E5E5");
        SetBrushColor(r, "HoverBackgroundBrush", dark ? "#383838" : "#F5F5F5");
        SetBrushColor(r, "PressedBackgroundBrush", dark ? "#434343" : "#E8E8E8");
        SetBrushColor(r, "SegmentBackgroundBrush", dark ? "#404040" : "#E0E0E0");
    }

    private static void SetBrushColor(ResourceDictionary r, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        r[key] = new SolidColorBrush(color);
    }

    #endregion

    #region Formatting Toolbar

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
    private void OnTable(object sender, RoutedEventArgs e) => Editor.InsertText("\n| Column 1 | Column 2 | Column 3 |\n| -------- | -------- | -------- |\n| Cell 1   | Cell 2   | Cell 3   |\n");
    private void OnHorizontalRule(object sender, RoutedEventArgs e) => Editor.InsertText("\n---\n");

    #endregion

    #region Sidebar

    private void OnToggleSidebar(object sender, RoutedEventArgs e)
    {
        _sidebarOpen = !_sidebarOpen;
        AnimateSidebar(_sidebarOpen ? SidebarWidth : 0);
    }

    private void AnimateSidebar(double targetWidth)
    {
        var anim = new DoubleAnimation
        {
            From = SidebarPanel.ActualWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(SidebarAnimMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        };
        SidebarPanel.BeginAnimation(FrameworkElement.WidthProperty, anim);
    }

    private void OnTabHistory(object sender, RoutedEventArgs e)
    {
        TabHistory.Tag = "Active";
        TabOutline.Tag = null;
        HistoryPanel.Visibility = Visibility.Visible;
        OutlinePanel.Visibility = Visibility.Collapsed;
    }

    private void OnTabOutline(object sender, RoutedEventArgs e)
    {
        TabOutline.Tag = "Active";
        TabHistory.Tag = null;
        OutlinePanel.Visibility = Visibility.Visible;
        HistoryPanel.Visibility = Visibility.Collapsed;
        UpdateOutline();
    }

    private void OnMarkdownChanged(object? sender, EventArgs e)
    {
        if (OutlinePanel.Visibility == Visibility.Visible)
            UpdateOutline();
    }

    private void AddToHistory(string filePath)
    {
        // Remove duplicate if already exists
        _fileHistory.RemoveAll(h => h.Path == filePath);
        _fileHistory.Insert(0, new FileHistoryEntry(filePath, DateTime.Now));

        // Keep max 20 entries
        if (_fileHistory.Count > 20)
            _fileHistory.RemoveRange(20, _fileHistory.Count - 20);

        UpdateHistoryList();
    }

    private void UpdateHistoryList()
    {
        HistoryList.Children.Clear();

        if (_fileHistory.Count == 0)
        {
            HistoryList.Children.Add(new TextBlock
            {
                Text = "No files opened yet",
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 32, 0, 0),
            });
            return;
        }

        foreach (var entry in _fileHistory)
        {
            var fileName = System.IO.Path.GetFileName(entry.Path);
            var dir = System.IO.Path.GetDirectoryName(entry.Path) ?? "";
            var timeStr = entry.OpenedAt.ToString("HH:mm");

            var btn = new Button
            {
                Style = (Style)FindResource("SidebarItemStyle"),
                Tag = entry.Path,
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = fileName,
                            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                            FontSize = 12,
                            FontWeight = FontWeights.Medium,
                        },
                        new TextBlock
                        {
                            Text = $"{timeStr} · {dir}",
                            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                            FontSize = 10,
                            Foreground = (Brush)FindResource("TextSecondaryBrush"),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            Margin = new Thickness(0, 2, 0, 0),
                        },
                    }
                },
            };
            btn.Click += OnHistoryItemClick;
            HistoryList.Children.Add(btn);
        }
    }

    private void OnHistoryItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string path) return;
        try
        {
            Editor.LoadFile(path);
            StatusText.Text = $"Loaded: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load file: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateOutline()
    {
        OutlineList.Children.Clear();

        var markdown = Editor.Markdown;
        if (string.IsNullOrEmpty(markdown))
        {
            OutlineList.Children.Add(new TextBlock
            {
                Text = "No headings found",
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
                Text = "No headings found",
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
        => _translationSettings ??= new TranslationSettingsService(AppContext.BaseDirectory);

    private void OnTranslatePopupOpened(object? sender, EventArgs e)
    {
        // First-run: if no provider is configured, auto-trigger config dialog
        var settings = GetTranslationSettings();
        var activeProvider = settings.GetActiveProvider();
        if (activeProvider == null || settings.LoadConfig(activeProvider)?.IsComplete != true)
        {
            TranslatePopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(isFirstRun: true);
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
            TranslatePopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(isFirstRun: false, preselectedProvider: newEngine, existingConfig: config);
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
        TranslatePopup.IsOpen = false;
        var settings = GetTranslationSettings();
        var activeProvider = settings.GetActiveProvider() ?? "Baidu";
        var existingConfig = settings.LoadConfig(activeProvider);
        var dialog = new TranslationConfigDialog(isFirstRun: false, preselectedProvider: activeProvider, existingConfig: existingConfig);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            settings.SaveConfig(dialog.SavedConfig!);
            settings.SetActiveProvider(dialog.SavedConfig!.ProviderName);
        }
    }

    private async Task TranslateDocumentAsync(TranslationLanguage targetLanguage)
    {
        if (_isTranslating) return;
        var settings = GetTranslationSettings();
        _lastTargetLanguage = targetLanguage;

        var activeProvider = settings.GetActiveProvider();
        if (activeProvider == null || settings.LoadConfig(activeProvider)?.IsComplete != true)
        {
            TranslatePopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(isFirstRun: activeProvider == null, preselectedProvider: activeProvider, existingConfig: activeProvider != null ? settings.LoadConfig(activeProvider) : null);
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

        _translationService = new TranslationService(provider);
        _isTranslating = true;
        CancelTranslateBtn.Visibility = Visibility.Visible;
        TranslateLanguagePanel.Visibility = Visibility.Collapsed;
        TranslatePopup.IsOpen = false;

        _progressOverlay = new TranslationProgressOverlay();
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
            StatusText.Text = $"Preview: {result.DetectedSourceLanguage.DisplayName()} → {targetLanguage.DisplayName()}";
            _progressOverlay.Hide();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Translation cancelled";
            _progressOverlay.Hide();
        }
        catch (TimeoutException ex)
        {
            _progressOverlay.ShowError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _progressOverlay.ShowError($"Network error: {ex.Message}");
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
        TranslatePopup.IsOpen = false;
    }

    private void OnClearTranslation(object sender, RoutedEventArgs e)
    {
        TranslatePopup.IsOpen = false;
        Editor.ClearTranslatedPreview();
        ClearTranslationBtn.Visibility = Visibility.Collapsed;
        StatusText.Text = "Translation cleared";
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
