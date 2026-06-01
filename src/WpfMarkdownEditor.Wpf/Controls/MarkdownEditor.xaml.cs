using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Wpf.Rendering;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.SyntaxHighlighting;
using WpfMarkdownEditor.Wpf.Theming;
using WpfMarkdownEditor.Wpf.Localization;

namespace WpfMarkdownEditor.Wpf.Controls;

/// <summary>
/// Embeddable Markdown editor with side-by-side preview.
/// </summary>
public partial class MarkdownEditor : UserControl, IDisposable
{
    private readonly MarkdownParser _parser = new();
    private readonly DispatcherTimer _debounceTimer;
    private readonly ImageLoader _imageLoader;
    private readonly SyntaxHighlighter _highlighter = new();
    private FlowDocumentRenderer? _renderer;
    private CancellationTokenSource? _cts;
    private int _renderVersion;
    private string? _translatedMarkdown;
    private LocalizationService? _localizationService;

    #region DependencyProperties

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnMarkdownChanged));

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(
            nameof(Theme),
            typeof(EditorTheme),
            typeof(MarkdownEditor),
            new PropertyMetadata(EditorTheme.Light, OnThemeChanged));

    public static readonly DependencyProperty ShowPreviewProperty =
        DependencyProperty.Register(
            nameof(ShowPreview),
            typeof(bool),
            typeof(MarkdownEditor),
            new PropertyMetadata(true));

    public static readonly DependencyProperty PreviewWidthProperty =
        DependencyProperty.Register(
            nameof(PreviewWidth),
            typeof(GridLength),
            typeof(MarkdownEditor),
            new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

    #endregion

    #region Public API

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public EditorTheme Theme
    {
        get => (EditorTheme)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public bool ShowPreview
    {
        get => (bool)GetValue(ShowPreviewProperty);
        set => SetValue(ShowPreviewProperty, value);
    }

    public GridLength PreviewWidth
    {
        get => (GridLength)GetValue(PreviewWidthProperty);
        set => SetValue(PreviewWidthProperty, value);
    }

    public event EventHandler<MarkdownChangedEventArgs>? MarkdownChanged;

    #endregion

    public MarkdownEditor()
    {
        InitializeComponent();
        _imageLoader = new ImageLoader(AppContext.BaseDirectory);
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _debounceTimer.Tick += OnDebounceTick;

        // Bind standard editing commands to the TextBox
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo));
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo));
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut));
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy));
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted));
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll));

        UpdateRenderer();
    }

    #region Methods

    public void LoadFile(string path) => Markdown = File.ReadAllText(path);

    public async Task SaveFileAsync(string path)
    {
        var content = Markdown;
        await Task.Run(() => File.WriteAllText(path, content));
    }

    public void ApplyTheme(EditorTheme theme) => Theme = theme;

    public void FocusEditor() => EditorTextBox.Focus();

    public TextBox TextBox => EditorTextBox;

    public void SetLocalizer(LocalizationService localizationService)
    {
        if (_localizationService != null)
        {
            WeakEventManager<LocalizationService, LanguageChangedEventArgs>.RemoveHandler(
                _localizationService,
                nameof(LocalizationService.LanguageChanged),
                OnLanguageChanged);
        }

        _localizationService = localizationService;
        WeakEventManager<LocalizationService, LanguageChangedEventArgs>.AddHandler(
            localizationService,
            nameof(LocalizationService.LanguageChanged),
            OnLanguageChanged);

        RefreshLocalizedText();
    }

    /// <summary>
    /// Wrap the selected text with before/after markers, or insert a placeholder.
    /// </summary>
    public void WrapSelection(string before, string after)
    {
        var textBox = EditorTextBox;
        var selectedText = textBox.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            var placeholder = "text";
            var insertionPos = textBox.SelectionStart;
            textBox.SelectedText = before + placeholder + after;
            textBox.Select(insertionPos + before.Length, placeholder.Length);
        }
        else
        {
            var selectionStart = textBox.SelectionStart;
            textBox.SelectedText = before + selectedText + after;
            textBox.Select(selectionStart + before.Length, selectedText.Length);
        }

        textBox.Focus();
    }

    /// <summary>
    /// Insert text at the cursor position.
    /// </summary>
    public void InsertText(string text)
    {
        var textBox = EditorTextBox;
        textBox.SelectedText = text;
        textBox.Focus();
    }

    /// <summary>
    /// Replace all text as a single undo unit. Used by translation feature.
    /// </summary>
    public void ReplaceAllText(string newText)
    {
        EditorTextBox.BeginChange();
        EditorTextBox.SelectAll();
        EditorTextBox.SelectedText = newText;
        EditorTextBox.EndChange();
    }

    /// <summary>
    /// Render translated markdown in the preview pane without changing editor text.
    /// </summary>
    public void RenderTranslatedPreview(string translatedMarkdown)
    {
        _translatedMarkdown = translatedMarkdown;
        RenderPreview();
    }

    /// <summary>
    /// Clear translated preview and revert to rendering the editor's content.
    /// </summary>
    public void ClearTranslatedPreview()
    {
        _translatedMarkdown = null;
        RenderPreview();
    }

    /// <summary>
    /// Toggle a line prefix (heading, quote, list marker) on the current line.
    /// If the line already has the prefix, it is removed. Otherwise it is added
    /// (replacing any existing heading prefix first).
    /// </summary>
    public void ToggleLinePrefix(string prefix)
    {
        var textBox = EditorTextBox;
        var text = textBox.Text;
        var caretIndex = textBox.CaretIndex;

        var lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1)) + 1;
        var lineEnd = text.IndexOf('\n', caretIndex);
        if (lineEnd < 0) lineEnd = text.Length;
        var lineText = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

        // Detect existing heading or block prefix
        var existingMatch = System.Text.RegularExpressions.Regex.Match(lineText, @"^(#{1,6}|>|-|\d+\.)\s");

        if (existingMatch.Success && existingMatch.Groups[1].Value == prefix.TrimEnd())
        {
            // Same prefix — remove it
            var removeLen = existingMatch.Value.Length;
            textBox.Select(lineStart, removeLen);
            textBox.SelectedText = "";
            textBox.CaretIndex = caretIndex > lineStart + removeLen
                ? caretIndex - removeLen
                : lineStart;
        }
        else
        {
            // Remove existing prefix if present, then insert new one
            var removeLen = existingMatch.Success ? existingMatch.Value.Length : 0;
            var replacement = prefix + " ";
            if (removeLen > 0)
            {
                textBox.Select(lineStart, removeLen);
                textBox.SelectedText = replacement;
                textBox.CaretIndex = caretIndex - removeLen + replacement.Length;
            }
            else
            {
                textBox.Select(lineStart, 0);
                textBox.SelectedText = replacement;
                textBox.CaretIndex = caretIndex + replacement.Length;
            }
        }

        textBox.Focus();
    }

    #endregion

    #region Private Implementation

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownEditor)d;
        editor._translatedMarkdown = null;
        editor.MarkdownChanged?.Invoke(editor, new MarkdownChangedEventArgs
        {
            OldMarkdown = (string?)e.OldValue ?? string.Empty,
            NewMarkdown = (string?)e.NewValue ?? string.Empty,
        });
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownEditor)d;
        var theme = (EditorTheme)e.NewValue;
        editor.UpdateRenderer();
        editor.RenderPreview();

        // Editor pane colors — use softer foreground for readability
        editor.EditorTextBox.Background = Frozen(theme.BackgroundColor);
        editor.EditorTextBox.Foreground = Frozen(theme.EditorForegroundColor);
        editor.EditorTextBox.CaretBrush = Frozen(theme.EditorCaretColor);
        editor.EditorTextBox.FontWeight = theme.EditorFontWeight;
        editor.EditorSplitter.Background = Frozen(theme.ThematicBreakColor);

        // Preview pane colors
        editor.PreviewViewer.Background = Frozen(theme.BackgroundColor);

        // Toolbar theme brushes
        var lum = 0.299 * theme.BackgroundColor.R + 0.587 * theme.BackgroundColor.G + 0.114 * theme.BackgroundColor.B;
        var isDark = lum < 128;
        editor.Resources["ToolbarBackground"] = Frozen(
            isDark ? Color.FromRgb(0x16, 0x1b, 0x22) : Color.FromRgb(0xf6, 0xf8, 0xfa));
        editor.Resources["ToolbarBorder"] = Frozen(
            isDark ? Color.FromRgb(0x30, 0x36, 0x3d) : Color.FromRgb(0xd0, 0xd7, 0xde));
        editor.Resources["ToolbarForeground"] = Frozen(
            isDark ? Color.FromRgb(0xe6, 0xed, 0xf3) : Color.FromRgb(0x24, 0x29, 0x2f));
        editor.Resources["ToolbarHoverBackground"] = Frozen(
            isDark ? Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x18, 0x00, 0x00, 0x00));
        editor.Resources["ToolbarPressedBackground"] = Frozen(
            isDark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x28, 0x00, 0x00, 0x00));
        editor.Resources["ToolbarAccent"] = Frozen(
            isDark ? Color.FromRgb(0x58, 0xa6, 0xff) : Color.FromRgb(0x09, 0x69, 0xda));

        // Toolbar border & separator
        editor.PreviewToolbarBorder.BorderBrush = Frozen(
            isDark ? Color.FromRgb(0x30, 0x36, 0x3d) : Color.FromRgb(0xd0, 0xd7, 0xde));
        editor.PreviewToolbarBorder.Background = Frozen(
            isDark ? Color.FromRgb(0x16, 0x1b, 0x22) : Color.FromRgb(0xf6, 0xf8, 0xfa));
        editor.ToolBarSeparator.Background = Frozen(
            isDark ? Color.FromRgb(0x30, 0x36, 0x3d) : Color.FromRgb(0xd0, 0xd7, 0xde));
        editor.ZoomText.Foreground = Frozen(
            isDark ? Color.FromRgb(0x8b, 0x94, 0x9e) : Color.FromRgb(0x65, 0x6d, 0x76));

        // Context menu theme
        editor.Resources["MenuBackground"] = Frozen(
            isDark ? Color.FromRgb(0x2d, 0x2d, 0x2d) : Color.FromRgb(0xff, 0xff, 0xff));
        editor.Resources["MenuBorder"] = Frozen(
            isDark ? Color.FromRgb(0x3d, 0x3d, 0x3d) : Color.FromRgb(0xe5, 0xe5, 0xe5));
        editor.Resources["MenuForeground"] = Frozen(
            isDark ? Color.FromRgb(0xff, 0xff, 0xff) : Color.FromRgb(0x1a, 0x1a, 0x1a));
        editor.Resources["MenuHoverBackground"] = Frozen(
            isDark ? Color.FromRgb(0x38, 0x38, 0x38) : Color.FromRgb(0xf5, 0xf5, 0xf5));
        editor.Resources["MenuSeparator"] = Frozen(
            isDark ? Color.FromRgb(0x3d, 0x3d, 0x3d) : Color.FromRgb(0xe5, 0xe5, 0xe5));
        editor.Resources["MenuDisabledForeground"] = Frozen(
            isDark ? Color.FromRgb(0x66, 0x66, 0x66) : Color.FromRgb(0xb0, 0xb0, 0xb0));
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => RefreshLocalizedText();

    private void RefreshLocalizedText()
    {
        if (_localizationService == null)
            return;

        UndoMenuItem.Header = _localizationService.GetString("Editor.Undo");
        RedoMenuItem.Header = _localizationService.GetString("Editor.Redo");
        CutMenuItem.Header = _localizationService.GetString("Editor.Cut");
        CopyMenuItem.Header = _localizationService.GetString("Editor.Copy");
        PasteMenuItem.Header = _localizationService.GetString("Editor.Paste");
        SelectAllMenuItem.Header = _localizationService.GetString("Editor.SelectAll");
        ZoomOutBtn.ToolTip = _localizationService.GetString("Editor.ZoomOut");
        ZoomInBtn.ToolTip = _localizationService.GetString("Editor.ZoomIn");
        ZoomResetBtn.ToolTip = _localizationService.GetString("Editor.ResetZoom");
    }

    private void UpdateRenderer()
    {
        _renderer = new FlowDocumentRenderer(Theme, _imageLoader, _highlighter);
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceTimer.Stop();
        SwapCts()?.Cancel();

        // Clear translated preview when user edits
        _translatedMarkdown = null;

        _debounceTimer.Start();
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        RenderPreview();
    }

    private async void RenderPreview()
    {
        var version = Interlocked.Increment(ref _renderVersion);
        var oldCts = SwapCts();
        oldCts?.Cancel();
        oldCts?.Dispose();
        var ct = _cts!.Token;

        try
        {
            var markdown = _translatedMarkdown ?? Markdown;
            var renderer = _renderer;
            if (renderer is null) return;

            var blocks = await Task.Run(() => _parser.Parse(markdown), ct);

            // Version check — discard stale results
            if (version != Volatile.Read(ref _renderVersion)) return;
            ct.ThrowIfCancellationRequested();

            var document = renderer.Render(blocks);

            // Final version check before UI update
            if (version != Volatile.Read(ref _renderVersion)) return;

            PreviewViewer.Document = document;
        }
        catch (OperationCanceledException)
        {
            // Newer render superseded this one
        }
        catch (Exception)
        {
            PreviewViewer.Document = BuildErrorDocument("Preview unavailable");
        }
    }

    private static FlowDocument BuildErrorDocument(string message)
    {
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run(message)) { FontStyle = FontStyles.Italic });
        return doc;
    }

    /// <summary>
    /// Atomically swap the CancellationTokenSource with a new one. Returns the old CTS for disposal.
    /// </summary>
    private CancellationTokenSource? SwapCts()
    {
        var newCts = new CancellationTokenSource();
        return Interlocked.Exchange(ref _cts, newCts);
    }

    #region Paste Image Path

    private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var textBox = EditorTextBox;

        // Priority 1: Clipboard image (screenshot, copied image)
        if (Clipboard.ContainsImage())
        {
            var imageSource = Clipboard.GetImage();
            if (imageSource != null)
            {
                var imagePath = SaveClipboardImage(imageSource);
                if (imagePath != null)
                {
                    textBox.SelectedText = CreateImageMarkdown(imagePath);
                    textBox.Focus();
                    e.Handled = true;
                    return;
                }
            }
        }

        // Priority 2: File drop list (copied image file from Explorer)
        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            foreach (string? file in files)
            {
                if (file == null) continue;
                var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg")
                {
                    textBox.SelectedText = CreateImageMarkdown(file);
                    textBox.Focus();
                    e.Handled = true;
                    return;
                }
            }
        }

        // Priority 3: Standard text paste
        if (Clipboard.ContainsText())
        {
            textBox.SelectedText = Clipboard.GetText();
            textBox.Focus();
            e.Handled = true;
        }
    }

    private static string CreateImageMarkdown(string imagePath)
    {
        var fileName = System.IO.Path.GetFileName(imagePath);
        var altText = EscapeImageAltText(fileName);
        var destination = FormatMarkdownImageDestination(imagePath);
        return $"![{altText}]({destination})";
    }

    private static string EscapeImageAltText(string value) =>
        value.Replace("\\", "\\\\")
            .Replace("[", "\\[")
            .Replace("]", "\\]");

    private static string FormatMarkdownImageDestination(string imagePath)
    {
        var normalized = imagePath.Replace('\\', '/')
            .Replace("<", "%3C")
            .Replace(">", "%3E");

        return normalized.Any(c => char.IsWhiteSpace(c) || c is '(' or ')')
            ? $"<{normalized}>"
            : normalized;
    }

    private static string? SaveClipboardImage(BitmapSource image)
    {
        try
        {
            var imagesDir = System.IO.Path.Combine(AppContext.BaseDirectory, "images");
            Directory.CreateDirectory(imagesDir);

            var fileName = $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var filePath = System.IO.Path.Combine(imagesDir, fileName);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = System.IO.File.Create(filePath);
            encoder.Save(stream);

            return $"images/{fileName}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }

    #endregion

    #region Zoom Controls

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        PreviewViewer.DecreaseZoom();
        SyncZoomUI();
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        PreviewViewer.IncreaseZoom();
        SyncZoomUI();
    }

    private void OnZoomReset(object sender, RoutedEventArgs e)
    {
        PreviewViewer.Zoom = 100;
        SyncZoomUI();
    }

    private void OnZoomSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewViewer == null || ZoomText == null) return;
        PreviewViewer.Zoom = e.NewValue;
        ZoomText.Text = $"{(int)e.NewValue}%";
    }

    private void SyncZoomUI()
    {
        var zoom = PreviewViewer.Zoom;
        ZoomSlider.Value = zoom;
        ZoomText.Text = $"{(int)zoom}%";
    }

    #endregion

    #region Smart List Editing

    private static readonly Regex ListMarkerRegex = new(@"^(\s*)([-*+]|\d+\.)\s", RegexOptions.Compiled);
    private const int IndentSize = 2;

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            HandleSmartEnter(e);
        else if (e.Key == Key.Tab)
            HandleSmartTab(e);
    }

    private void HandleSmartEnter(KeyEventArgs e)
    {
        var textBox = EditorTextBox;
        var caretIndex = textBox.CaretIndex;
        var text = textBox.Text;

        var lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1)) + 1;
        var lineEnd = text.IndexOf('\n', caretIndex);
        if (lineEnd < 0) lineEnd = text.Length;
        var lineText = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

        var match = ListMarkerRegex.Match(lineText);
        if (!match.Success)
            return; // Not a list item — let default Enter handle it

        var indent = match.Groups[1].Value;
        var marker = match.Groups[2].Value;
        var markerEnd = match.Index + match.Length;

        // Only auto-continue if cursor is after the marker
        if (caretIndex < lineStart + markerEnd)
            return;

        e.Handled = true;

        var contentAfterMarker = lineText.Substring(markerEnd);
        if (string.IsNullOrWhiteSpace(contentAfterMarker))
        {
            // Empty list item — clear marker, insert plain newline
            textBox.Select(lineStart + indent.Length, lineText.Length - indent.Length);
            textBox.SelectedText = "";
            var insertPos = lineStart + indent.Length;
            textBox.Select(insertPos, 0);
            textBox.SelectedText = Environment.NewLine;
            textBox.CaretIndex = insertPos + Environment.NewLine.Length;
        }
        else
        {
            // Non-empty — insert newline with list marker at cursor
            var nextMarker = GetNextMarker(marker);
            var insertion = Environment.NewLine + indent + nextMarker + " ";
            textBox.Select(caretIndex, 0);
            textBox.SelectedText = insertion;
            textBox.CaretIndex = caretIndex + insertion.Length;
        }
    }

    private void HandleSmartTab(KeyEventArgs e)
    {
        var textBox = EditorTextBox;
        var text = textBox.Text;
        var caretIndex = textBox.CaretIndex;

        var lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1)) + 1;
        var lineEnd = text.IndexOf('\n', caretIndex);
        if (lineEnd < 0) lineEnd = text.Length;
        var lineText = text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

        // Only intercept Tab for list item lines
        if (!ListMarkerRegex.IsMatch(lineText))
            return; // Let default Tab behavior (AcceptsTab handles it)

        e.Handled = true;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            // Outdent: remove up to IndentSize spaces from line start
            var leadingSpaces = lineText.Length - lineText.TrimStart(' ').Length;
            var spacesToRemove = Math.Min(IndentSize, leadingSpaces);
            if (spacesToRemove > 0)
            {
                textBox.Select(lineStart, spacesToRemove);
                textBox.SelectedText = "";
                textBox.CaretIndex = caretIndex > lineStart + spacesToRemove
                    ? caretIndex - spacesToRemove
                    : lineStart;
            }
        }
        else
        {
            // Indent: insert spaces at line start
            var spaces = new string(' ', IndentSize);
            textBox.Select(lineStart, 0);
            textBox.SelectedText = spaces;
            textBox.CaretIndex = caretIndex + IndentSize;
        }
    }

    private static string GetNextMarker(string currentMarker)
    {
        if (int.TryParse(currentMarker[..^1], out var number))
            return $"{number + 1}.";
        return currentMarker; // Unordered: keep same marker (-, *, +)
    }

    #endregion

    public void Dispose()
    {
        if (_localizationService != null)
        {
            WeakEventManager<LocalizationService, LanguageChangedEventArgs>.RemoveHandler(
                _localizationService,
                nameof(LocalizationService.LanguageChanged),
                OnLanguageChanged);
        }

        _imageLoader.Dispose();
        _debounceTimer.Stop();
        var oldCts = Interlocked.Exchange(ref _cts, null);
        oldCts?.Cancel();
        oldCts?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}

public sealed class MarkdownChangedEventArgs : EventArgs
{
    public string OldMarkdown { get; init; } = string.Empty;
    public string NewMarkdown { get; init; } = string.Empty;
}
