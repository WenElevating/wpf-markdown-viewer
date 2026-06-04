using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
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
    private static readonly object PreviewErrorTag = new();

    private readonly MarkdownParser _parser = new();
    private readonly DispatcherTimer _debounceTimer;
    private ImageLoader _imageLoader;
    private readonly SyntaxHighlighter _highlighter = new();
    private FlowDocumentRenderer? _renderer;
    private CancellationTokenSource? _cts;
    private int _renderVersion;
    private FlowDocument? _previewDocument;
    private bool _layoutRefreshQueued;
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

    public static readonly DependencyProperty DocumentPathProperty =
        DependencyProperty.Register(
            nameof(DocumentPath),
            typeof(string),
            typeof(MarkdownEditor),
            new PropertyMetadata(null, OnDocumentPathChanged));

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

    public string? DocumentPath
    {
        get => (string?)GetValue(DocumentPathProperty);
        set => SetValue(DocumentPathProperty, value);
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
        EditorTextBox.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, OnSelectAllExecuted));

        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.PasteImage, OnPasteImageExecuted, OnCanPasteImage));
        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.CopyPlainText, OnCopyPlainTextExecuted, OnCanCopyPlainText));
        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.PastePlainText, OnPastePlainTextExecuted, OnCanPastePlainText));
        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.MoveLineUp, OnMoveLineUpExecuted, OnCanMoveLineUp));
        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.MoveLineDown, OnMoveLineDownExecuted, OnCanMoveLineDown));
        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.DeleteSelectionOrCurrentLine, OnDeleteSelectionOrCurrentLineExecuted, OnCanDeleteSelectionOrCurrentLine));
        CommandBindings.Add(new CommandBinding(MarkdownEditorCommands.InsertHardLineBreak, OnInsertHardLineBreakExecuted));

        RefreshAutomationProperties(FallbackStringLocalizer.Instance);
        UpdateRenderer();
    }

    #region Methods

    public void LoadFile(string path)
    {
        var markdown = File.ReadAllText(path);
        DocumentPath = path;
        Markdown = markdown;
    }

    public async Task SaveFileAsync(string path)
    {
        var content = Markdown;
        await Task.Run(() => File.WriteAllText(path, content));
    }

    public void ApplyTheme(EditorTheme theme) => Theme = theme;

    public void FocusEditor() => EditorTextBox.Focus();

    public TextBox TextBox => EditorTextBox;

    public void AppendMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content))
            return;

        var current = Markdown;
        if (!string.IsNullOrEmpty(current) && !current.EndsWith("\n", StringComparison.Ordinal))
            current += Environment.NewLine + Environment.NewLine;

        var updated = current + content;
        EditorTextBox.Text = updated;
        Markdown = updated;
        EditorTextBox.CaretIndex = Markdown.Length;
        EditorTextBox.Focus();
    }

    public bool TryGetPrintablePreviewDocument(out FlowDocument document)
    {
        document = null!;
        if (!ShowPreview)
            return false;

        var candidate = PreviewViewer.Document;
        if (candidate is null || candidate.Blocks.Count == 0 || ReferenceEquals(candidate.Tag, PreviewErrorTag))
            return false;

        document = candidate;
        return true;
    }

    public FlowDocument CreatePlainTextPrintDocument()
    {
        var document = new FlowDocument();
        document.Blocks.Add(new Paragraph(new Run(Markdown)));
        return document;
    }

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
        editor.SchedulePreviewRender();
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

    private static void OnDocumentPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (MarkdownEditor)d;
        editor.UpdateImageLoaderBaseDirectory();
        editor.UpdateRenderer();
        editor.SchedulePreviewRender();
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
        RefreshAutomationProperties(_localizationService);
    }

    private void RefreshAutomationProperties(IStringLocalizer localizer)
    {
        AutomationProperties.SetName(this, localizer.GetString("Editor.MarkdownEditor"));
        AutomationProperties.SetName(EditorTextBox, localizer.GetString("Editor.MarkdownSource"));
        AutomationProperties.SetName(EditorSplitter, localizer.GetString("Editor.Splitter"));
        AutomationProperties.SetName(PreviewViewer, localizer.GetString("Editor.Preview"));
        AutomationProperties.SetName(ZoomOutBtn, localizer.GetString("Editor.ZoomOut"));
        AutomationProperties.SetName(ZoomSlider, localizer.GetString("Editor.ZoomLevel"));
        AutomationProperties.SetName(ZoomText, localizer.GetString("Editor.ZoomLevel"));
        AutomationProperties.SetName(ZoomInBtn, localizer.GetString("Editor.ZoomIn"));
        AutomationProperties.SetName(ZoomResetBtn, localizer.GetString("Editor.ResetZoom"));
    }

    private void UpdateRenderer()
    {
        _previewDocument = null;
        _renderer = new FlowDocumentRenderer(Theme, _imageLoader, _highlighter, RequestPreviewLayoutRefresh);
    }

    private void UpdateImageLoaderBaseDirectory()
    {
        var previous = _imageLoader;
        _imageLoader = new ImageLoader(GetDocumentBaseDirectory());
        previous.Dispose();
    }

    private string GetDocumentBaseDirectory()
    {
        if (string.IsNullOrWhiteSpace(DocumentPath))
            return AppContext.BaseDirectory;

        try
        {
            var fullPath = System.IO.Path.GetFullPath(DocumentPath);
            return System.IO.Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return AppContext.BaseDirectory;
        }
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        _translatedMarkdown = null;
        SchedulePreviewRender();
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

            var renderIncrementally = _translatedMarkdown is null;
            var existingDocument = renderIncrementally ? _previewDocument : null;
            if (existingDocument is not null && ReferenceEquals(PreviewViewer.Document, existingDocument))
                PreviewViewer.Document = null;

            var document = renderIncrementally
                ? renderer.RenderIncremental(existingDocument, blocks, markdown)
                : renderer.Render(blocks);

            // Final version check before UI update
            if (version != Volatile.Read(ref _renderVersion)) return;

            _previewDocument = document;
            PreviewViewer.Document = document;
        }
        catch (OperationCanceledException)
        {
            // Newer render superseded this one
        }
        catch (Exception)
        {
            _previewDocument = BuildErrorDocument("Preview unavailable");
            PreviewViewer.Document = _previewDocument;
        }
    }

    private void SchedulePreviewRender()
    {
        _debounceTimer.Stop();
        var oldCts = SwapCts();
        oldCts?.Cancel();
        oldCts?.Dispose();
        _debounceTimer.Start();
    }

    private void RequestPreviewLayoutRefresh()
    {
        if (_layoutRefreshQueued)
            return;

        _layoutRefreshQueued = true;
        PreviewViewer.Dispatcher.BeginInvoke(
            DispatcherPriority.Render,
            new Action(() =>
            {
                _layoutRefreshQueued = false;
                var document = PreviewViewer.Document;
                PreviewViewer.InvalidateMeasure();
                PreviewViewer.InvalidateArrange();
                PreviewViewer.InvalidateVisual();
                PreviewViewer.UpdateLayout();
            }));
    }

    private static FlowDocument BuildErrorDocument(string message)
    {
        var doc = new FlowDocument { Tag = PreviewErrorTag };
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

        if (TryPasteImageFromClipboard())
        {
            e.Handled = true;
            return;
        }

        // Standard text paste fallback
        if (Clipboard.ContainsText())
        {
            textBox.SelectedText = Clipboard.GetText();
            textBox.Focus();
            e.Handled = true;
        }
    }

    private void OnSelectAllExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        EditorTextBox.SelectAll();
        EditorTextBox.Focus();
        e.Handled = true;
    }

    private void OnCanPasteImage(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ClipboardHasImageSource();
        e.Handled = true;
    }

    private void OnPasteImageExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = TryPasteImageFromClipboard();
    }

    private void OnCanCopyPlainText(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = EditorTextBox.SelectionLength > 0;
        e.Handled = true;
    }

    private void OnCopyPlainTextExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (EditorTextBox.SelectionLength <= 0)
            return;

        Clipboard.SetText(EditorTextBox.SelectedText, TextDataFormat.UnicodeText);
        e.Handled = true;
    }

    private void OnCanPastePlainText(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ClipboardContainsUnicodeText();
        e.Handled = true;
    }

    private void OnPastePlainTextExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ClipboardContainsUnicodeText())
            return;

        var operation = EditorTextOperations.InsertText(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            Clipboard.GetText(TextDataFormat.UnicodeText));
        ApplyTextOperation(operation);
        e.Handled = true;
    }

    private void OnCanMoveLineUp(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = EditorTextOperations.MoveSelectedLines(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            -1) is not null;
        e.Handled = true;
    }

    private void OnMoveLineUpExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var operation = EditorTextOperations.MoveSelectedLines(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            -1);
        if (operation is null)
            return;

        ApplyTextOperation(operation.Value);
        e.Handled = true;
    }

    private void OnCanMoveLineDown(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = EditorTextOperations.MoveSelectedLines(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            1) is not null;
        e.Handled = true;
    }

    private void OnMoveLineDownExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var operation = EditorTextOperations.MoveSelectedLines(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            1);
        if (operation is null)
            return;

        ApplyTextOperation(operation.Value);
        e.Handled = true;
    }

    private void OnCanDeleteSelectionOrCurrentLine(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = EditorTextOperations.DeleteSelectionOrCurrentLine(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength) is not null;
        e.Handled = true;
    }

    private void OnDeleteSelectionOrCurrentLineExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var operation = EditorTextOperations.DeleteSelectionOrCurrentLine(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength);
        if (operation is null)
            return;

        ApplyTextOperation(operation.Value);
        e.Handled = true;
    }

    private void OnInsertHardLineBreakExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var operation = EditorTextOperations.InsertText(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            "  " + Environment.NewLine);
        ApplyTextOperation(operation);
        e.Handled = true;
    }

    private void ApplyTextOperation(TextEditOperation operation)
    {
        EditorTextBox.BeginChange();
        try
        {
            EditorTextBox.Text = operation.Text;
            Markdown = operation.Text;
            var selectionStart = Math.Clamp(operation.SelectionStart, 0, EditorTextBox.Text.Length);
            var selectionLength = Math.Clamp(operation.SelectionLength, 0, EditorTextBox.Text.Length - selectionStart);
            EditorTextBox.Select(selectionStart, selectionLength);
        }
        finally
        {
            EditorTextBox.EndChange();
        }

        EditorTextBox.Focus();
    }

    private static bool ClipboardContainsUnicodeText()
    {
        try
        {
            return Clipboard.ContainsText(TextDataFormat.UnicodeText);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return false;
        }
    }

    private static bool ClipboardHasImageSource()
    {
        try
        {
            if (Clipboard.ContainsImage())
                return true;

            if (!Clipboard.ContainsFileDropList())
                return false;

            var files = Clipboard.GetFileDropList();
            return files.Cast<string?>().Any(IsSupportedImagePath);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return false;
        }
    }

    private bool TryPasteImageFromClipboard()
    {
        // Priority 1: Clipboard image (screenshot, copied image)
        if (Clipboard.ContainsImage())
        {
            var imageSource = Clipboard.GetImage();
            if (imageSource != null)
            {
                var imagePath = SaveClipboardImage(imageSource, GetDocumentBaseDirectory());
                if (imagePath != null)
                {
                    InsertImageMarkdown(CreateImageMarkdown(imagePath));
                    return true;
                }
            }
        }

        // Priority 2: File drop list (copied image file from Explorer)
        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            foreach (string? file in files)
            {
                if (IsSupportedImagePath(file))
                {
                    InsertImageMarkdown(CreateImageMarkdown(file!));
                    return true;
                }
            }
        }

        return false;
    }

    private void InsertImageMarkdown(string markdown)
    {
        var operation = EditorTextOperations.InsertImageBlock(
            EditorTextBox.Text,
            EditorTextBox.SelectionStart,
            EditorTextBox.SelectionLength,
            markdown);
        ApplyTextOperation(operation);
        UpdateRenderer();
        RenderPreview();
    }

    private static bool IsSupportedImagePath(string? file)
    {
        if (file == null) return false;
        var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg";
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

    internal static string? SaveClipboardImage(BitmapSource image, string baseDirectory)
    {
        try
        {
            var imagesDir = System.IO.Path.Combine(baseDirectory, "images");
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
