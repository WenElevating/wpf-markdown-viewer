using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        editor.UpdateRenderer();
        editor.RenderPreview();
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
