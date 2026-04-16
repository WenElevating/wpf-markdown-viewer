using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Wpf.Rendering;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.SyntaxHighlighting;
using WpfMarkdownEditor.Wpf.Theming;

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
        _imageLoader = new ImageLoader();
        _debounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _debounceTimer.Tick += OnDebounceTick;
        UpdateRenderer();
    }

    #region Methods

    public void LoadFile(string path) => Markdown = File.ReadAllText(path);

    public async Task SaveFileAsync(string path)
    {
        await Task.Run(() => File.WriteAllText(path, Markdown));
    }

    public void ApplyTheme(EditorTheme theme) => Theme = theme;

    public void FocusEditor() => EditorTextBox.Focus();

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
            textBox.SelectedText = before + placeholder + after;
            var start = textBox.CaretIndex - before.Length - placeholder.Length - after.Length + before.Length;
            textBox.Select(start, placeholder.Length);
        }
        else
        {
            textBox.SelectedText = before + selectedText + after;
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
        textBox.CaretIndex += 0; // Keep cursor after insertion
        textBox.Focus();
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

        // Editor pane colors
        editor.EditorTextBox.Background = new SolidColorBrush(theme.BackgroundColor);
        editor.EditorTextBox.Foreground = new SolidColorBrush(theme.ForegroundColor);
        editor.EditorSplitter.Background = new SolidColorBrush(theme.ThematicBreakColor);

        // Preview pane colors
        editor.PreviewReader.Background = new SolidColorBrush(theme.BackgroundColor);

        // Toolbar brushes — semi-transparent overlays adapt to both themes
        var isDark = theme.BackgroundColor.R < 128;
        editor.Resources["PreviewToolbarForeground"] = new SolidColorBrush(
            isDark ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x61, 0x61, 0x61));
        editor.Resources["PreviewToolbarHover"] = new SolidColorBrush(
            isDark ? Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x18, 0x00, 0x00, 0x00));
        editor.Resources["PreviewToolbarPressed"] = new SolidColorBrush(
            isDark ? Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x28, 0x00, 0x00, 0x00));
        editor.Resources["PreviewToolbarAccent"] = new SolidColorBrush(
            isDark ? Color.FromRgb(0x60, 0xCD, 0xFF) : Color.FromRgb(0x00, 0x5F, 0xB8));
    }

    private void UpdateRenderer()
    {
        _renderer = new FlowDocumentRenderer(Theme, _imageLoader, _highlighter);
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceTimer.Stop();
        SwapCts()?.Cancel();
        _debounceTimer.Start();
    }

    private async void OnDebounceTick(object? sender, EventArgs e)
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
            var markdown = Markdown;
            var renderer = _renderer;
            if (renderer is null) return;

            var blocks = await Task.Run(() => _parser.Parse(markdown), ct);

            // Version check — discard stale results
            if (version != Volatile.Read(ref _renderVersion)) return;
            ct.ThrowIfCancellationRequested();

            var document = renderer.Render(blocks);

            // Final version check before UI update
            if (version != Volatile.Read(ref _renderVersion)) return;

            PreviewReader.Document = document;
        }
        catch (OperationCanceledException)
        {
            // Newer render superseded this one
        }
    }

    /// <summary>
    /// Atomically swap the CancellationTokenSource with a new one. Returns the old CTS for disposal.
    /// </summary>
    private CancellationTokenSource? SwapCts()
    {
        var newCts = new CancellationTokenSource();
        return Interlocked.Exchange(ref _cts, newCts);
    }

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
        if (int.TryParse(currentMarker.TrimEnd('.'), out var number))
            return $"{number + 1}.";
        return currentMarker; // Unordered: keep same marker (-, *, +)
    }

    #endregion

    public void Dispose()
    {
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
