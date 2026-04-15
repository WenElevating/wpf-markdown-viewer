using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Core.Parsing.Inlines;
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
    private bool _suppressDirtyTracking;

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

    public static readonly DependencyProperty IsDirtyProperty =
        DependencyProperty.Register(
            nameof(IsDirty),
            typeof(bool),
            typeof(MarkdownEditor),
            new PropertyMetadata(false));

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

    public bool IsDirty
    {
        get => (bool)GetValue(IsDirtyProperty);
        set => SetValue(IsDirtyProperty, value);
    }

    public event EventHandler<MarkdownChangedEventArgs>? MarkdownChanged;
    public event EventHandler? IsDirtyChanged;
    public event EventHandler<Events.OutlineChangedEventArgs>? OutlineChanged;

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

    public void LoadFile(string path)
    {
        _suppressDirtyTracking = true;
        try
        {
            Markdown = File.ReadAllText(path);
            MarkAsSaved();
        }
        finally
        {
            _suppressDirtyTracking = false;
        }
    }

    public async Task SaveFileAsync(string path)
    {
        await Task.Run(() => File.WriteAllText(path, Markdown));
        MarkAsSaved();
    }

    public void MarkAsSaved()
    {
        IsDirty = false;
        IsDirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTheme(EditorTheme theme) => Theme = theme;

    public void FocusEditor() => EditorTextBox.Focus();

    public void WrapSelection(string prefix, string suffix)
    {
        var textBox = EditorTextBox;
        textBox.BeginChange();
        try
        {
            if (textBox.SelectionLength > 0)
            {
                var selected = textBox.SelectedText;
                textBox.SelectedText = prefix + selected + suffix;
            }
            else
            {
                var caretIndex = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caretIndex, prefix + suffix);
                textBox.CaretIndex = caretIndex + prefix.Length;
            }
        }
        finally { textBox.EndChange(); }
    }

    public void InsertAtCursor(string text)
    {
        var textBox = EditorTextBox;
        textBox.BeginChange();
        try
        {
            var caretIndex = textBox.CaretIndex;
            textBox.Text = textBox.Text.Insert(caretIndex, text);
            textBox.CaretIndex = caretIndex + text.Length;
        }
        finally { textBox.EndChange(); }
    }

    public (int Line, int Column) GetCursorPosition()
    {
        var text = EditorTextBox.Text;
        var caretIndex = EditorTextBox.CaretIndex;
        int line = 1, col = 1;
        for (int i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n') { line++; col = 1; }
            else col++;
        }
        return (line, col);
    }

    public (int Start, int Length) GetSelectionRange()
    {
        return (EditorTextBox.SelectionStart, EditorTextBox.SelectionLength);
    }

    public void ScrollToLine(int lineNumber)
    {
        EditorTextBox.ScrollToLine(lineNumber - 1);
        EditorTextBox.Focus();
    }

    #endregion

    #region Private Implementation

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownEditor)d;
        var oldValue = (string?)e.OldValue ?? string.Empty;
        var newValue = (string?)e.NewValue ?? string.Empty;

        editor.MarkdownChanged?.Invoke(editor, new MarkdownChangedEventArgs
        {
            OldMarkdown = oldValue,
            NewMarkdown = newValue,
        });

        if (!editor._suppressDirtyTracking && oldValue != newValue)
        {
            editor.IsDirty = true;
            editor.IsDirtyChanged?.Invoke(editor, EventArgs.Empty);
        }
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
        editor.EditorTextBox.CaretBrush = new SolidColorBrush(theme.CursorColor);

        // Toolbar brushes — semi-transparent overlays adapt to both themes
        var isDark = theme.BackgroundColor.R < 128;

        // Splitter colors
        var splitterColor = isDark
            ? Color.FromRgb(0x3A, 0x3A, 0x3A)
            : Color.FromRgb(0xD8, 0xD8, 0xD8);
        editor.Resources["EditorSplitterBrush"] = new SolidColorBrush(splitterColor);
        editor.Resources["EditorSplitterHoverBrush"] = new SolidColorBrush(theme.LinkColor);

        // Preview pane colors
        editor.PreviewReader.Background = new SolidColorBrush(theme.BackgroundColor);
        editor.Resources["PreviewToolbarForeground"] = new SolidColorBrush(
            isDark ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x61, 0x61, 0x61));
        editor.Resources["PreviewToolbarHover"] = new SolidColorBrush(
            isDark ? Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x18, 0x00, 0x00, 0x00));
        editor.Resources["PreviewToolbarPressed"] = new SolidColorBrush(
            isDark ? Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x28, 0x00, 0x00, 0x00));
        editor.Resources["PreviewToolbarAccent"] = new SolidColorBrush(
            isDark ? Color.FromRgb(0x60, 0xCD, 0xFF) : Color.FromRgb(0x00, 0x5F, 0xB8));
    }

    private static string ExtractInlineText(List<Inline> inlines)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline t: sb.Append(t.Content); break;
                case CodeInline c: sb.Append(c.Code); break;
                case ImageInline i: sb.Append(i.Alt ?? ""); break;
                case LinkInline l: sb.Append(ExtractInlineText(l.Children)); break;
                case BoldInline b: sb.Append(ExtractInlineText(b.Children)); break;
                case ItalicInline i: sb.Append(ExtractInlineText(i.Children)); break;
                case BoldItalicInline bi: sb.Append(ExtractInlineText(bi.Children)); break;
                case StrikethroughInline s: sb.Append(ExtractInlineText(s.Children)); break;
                case LineBreakInline: break;
            }
        }
        return sb.ToString();
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

            // Extract outline from parsed blocks (no additional parsing)
            var headingBlocks = blocks.OfType<WpfMarkdownEditor.Core.Parsing.Blocks.HeadingBlock>().ToList();
            if (headingBlocks.Count > 0)
            {
                var outline = headingBlocks.Select(h => new Models.OutlineItem
                {
                    Level = h.Level,
                    Text = ExtractInlineText(h.Inlines),
                    LineNumber = h.LineStart
                }).ToList();
                OutlineChanged?.Invoke(this, new Events.OutlineChangedEventArgs { Outline = outline });
            }

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
