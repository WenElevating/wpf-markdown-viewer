# WPF Markdown Editor Control - Design Specification

**Date**: 2026-04-13
**Status**: Approved
**Target**: .NET 8 + WPF

---

## Overview

A high-performance, themable WPF UserControl for real-time Markdown editing with side-by-side preview. Designed as an embeddable component for integration into other WPF applications.

### Key Requirements

- **Embeddable**: UserControl, not standalone application
- **Real-time preview**: Side-by-side layout with <50ms update latency
- **Custom rendering**: Markdown-to-WPF FlowDocument converter (no HTML/WebView2)
- **Full MD support**: Headings, paragraphs, code blocks, tables, blockquotes, lists, images
- **Themable**: Light/dark themes with customizable colors, fonts, spacing
- **Zero dependencies**: Pure .NET 8, no external NuGet packages

---

## Architecture

### High-Level Pipeline

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│   TextBox       │────▶│  MarkdownParser  │────▶│ FlowDocumentRenderer│
│   (Editor)      │     │  (Core)          │     │ (WPF)               │
└─────────────────┘     └──────────────────┘     └─────────────────────┘
        │                       │                          │
        │                       ▼                          ▼
        │               ┌──────────────────┐     ┌─────────────────────┐
        │               │  Block AST       │     │  FlowDocument       │
        │               │  (Core)          │     │  (Preview)          │
        │               └──────────────────┘     └─────────────────────┘
        │                                                  │
        └──────────────────────────────────────────────────┘
                      Two-way binding via DependencyProperty
```

### Project Structure

```
WpfMarkdownEditor/
├── src/
│   ├── WpfMarkdownEditor.Core/           # Parser & AST (no WPF deps)
│   │   ├── Parsing/
│   │   │   ├── MarkdownParser.cs         # MD text → Block AST
│   │   │   ├── Lexer.cs                  # Text → tokens (optional, may inline)
│   │   │   ├── Block.cs                  # Abstract base
│   │   │   ├── Inline.cs                 # Abstract base
│   │   │   └── Blocks/                   # Block implementations
│   │   │       ├── HeadingBlock.cs
│   │   │       ├── ParagraphBlock.cs
│   │   │       ├── CodeBlock.cs
│   │   │       ├── TableBlock.cs
│   │   │       ├── BlockquoteBlock.cs
│   │   │       ├── ListBlock.cs
│   │   │       ├── ThematicBreakBlock.cs
│   │   │       └── ImageBlock.cs
│   │   └── Inlines/                      # Inline implementations (in Parsing namespace)
│   │       ├── TextInline.cs
│   │       ├── BoldInline.cs
│   │       ├── ItalicInline.cs
│   │       ├── CodeInline.cs
│   │       ├── LinkInline.cs
│   │       └── ImageInline.cs
│   │
│   └── WpfMarkdownEditor.Wpf/            # WPF control library
│       ├── Controls/
│       │   ├── MarkdownEditor.xaml       # Main control
│       │   ├── MarkdownEditor.xaml.cs
│       │   ├── MarkdownPreview.xaml      # Preview-only control
│       │   └── MarkdownPreview.xaml.cs
│       ├── Rendering/
│       │   ├── FlowDocumentRenderer.cs   # AST → FlowDocument
│       │   ├── IBlockRenderer.cs         # Renderer interface
│       │   └── Renderers/                # Per-block renderers
│       │       ├── HeadingRenderer.cs
│       │       ├── ParagraphRenderer.cs
│       │       ├── CodeBlockRenderer.cs
│       │       ├── TableRenderer.cs
│       │       ├── BlockquoteRenderer.cs
│       │       ├── ListRenderer.cs
│       │       └── ImageRenderer.cs
│       ├── Themes/
│       │   ├── EditorTheme.cs            # Theme definition (colors, fonts)
│       │   ├── LightTheme.xaml           # Built-in light theme
│       │   └── DarkTheme.xaml            # Built-in dark theme
│       └── Converters/
│           └── BoolToVisibilityConverter.cs  # Boolean to Visibility converter
│
└── tests/
    ├── WpfMarkdownEditor.Core.Tests/     # Parser unit tests
    └── WpfMarkdownEditor.Wpf.Tests/      # Renderer & control tests
```

---

## Component Details

### 1. AST Model (Core)

#### Block Types

```csharp
namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Base class for all block-level elements.
/// </summary>
public abstract class Block
{
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
}

/// <summary>
/// Heading block (H1-H6).
/// </summary>
public sealed class HeadingBlock : Block
{
    public int Level { get; set; }           // 1-6
    public List<Inline> Inlines { get; set; } = [];
}

/// <summary>
/// Paragraph with inline formatting.
/// </summary>
public sealed class ParagraphBlock : Block
{
    public List<Inline> Inlines { get; set; } = [];
}

/// <summary>
/// Fenced or indented code block.
/// </summary>
public sealed class CodeBlock : Block
{
    public string? Language { get; set; }
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Markdown table.
/// </summary>
public sealed class TableBlock : Block
{
    public List<string> Headers { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
    public List<TableAlignment> Alignments { get; set; } = [];

    public enum TableAlignment { Left, Center, Right }
}

/// <summary>
/// Blockquote container.
/// </summary>
public sealed class BlockquoteBlock : Block
{
    public List<Block> Children { get; set; } = [];
}

/// <summary>
/// Ordered or unordered list.
/// </summary>
public sealed class ListBlock : Block
{
    public bool IsOrdered { get; set; }
    public List<ListItem> Items { get; set; } = [];
}

public sealed class ListItem
{
    public List<Block> Blocks { get; set; } = [];
}

/// <summary>
/// Horizontal rule (thematic break).
/// </summary>
public sealed class ThematicBreakBlock : Block { }

/// <summary>
/// Standalone image (not inline).
/// </summary>
public sealed class ImageBlock : Block
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public string? Title { get; set; }
}
```

#### Inline Types

```csharp
namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Base class for inline elements within blocks.
/// </summary>
public abstract class Inline { }

public sealed class TextInline : Inline
{
    public string Content { get; set; } = string.Empty;
}

public sealed class BoldInline : Inline
{
    public List<Inline> Children { get; set; } = [];
}

public sealed class ItalicInline : Inline
{
    public List<Inline> Children { get; set; } = [];
}

public sealed class BoldItalicInline : Inline
{
    public List<Inline> Children { get; set; } = [];
}

public sealed class CodeInline : Inline
{
    public string Code { get; set; } = string.Empty;
}

public sealed class LinkInline : Inline
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public List<Inline> Children { get; set; } = [];
}

public sealed class ImageInline : Inline
{
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public string? Title { get; set; }
}
```

---

### 2. Parser (Core)

```csharp
namespace WpfMarkdownEditor.Core.Parsing;

/// <summary>
/// Parses Markdown text into a Block AST.
/// Implements CommonMark spec with GFM extensions (tables, strikethrough).
/// </summary>
public sealed class MarkdownParser
{
    /// <summary>
    /// Parse Markdown text into a list of blocks.
    /// </summary>
    public List<Block> Parse(string markdown)
    {
        // Implementation:
        // 1. Split into lines
        // 2. Process line-by-line, tracking state (in code block, in list, etc.)
        // 3. Identify block boundaries
        // 4. Parse inlines within each block
        // 5. Return block list
    }

    /// <summary>
    /// Parse only inline elements within a text range.
    /// </summary>
    public List<Inline> ParseInlines(string text)
    {
        // Handle: **bold**, *italic*, `code`, [links](url), ![images](url)
    }
}
```

**Parsing Strategy:**
- Line-by-line processing with state machine
- Handle nested structures (blockquotes, lists) via stack
- Inline parsing with delimiter stack algorithm (CommonMark spec)

---

### 3. FlowDocument Renderer (WPF)

```csharp
namespace WpfMarkdownEditor.Wpf.Rendering;

/// <summary>
/// Renders Block AST to WPF FlowDocument.
/// </summary>
public sealed class FlowDocumentRenderer
{
    private readonly Dictionary<Type, IBlockRenderer> _renderers;
    private readonly EditorTheme _theme;

    public FlowDocumentRenderer(EditorTheme theme)
    {
        _theme = theme;
        _renderers = new()
        {
            [typeof(HeadingBlock)] = new HeadingRenderer(theme),
            [typeof(ParagraphBlock)] = new ParagraphRenderer(theme),
            [typeof(CodeBlock)] = new CodeBlockRenderer(theme),
            [typeof(TableBlock)] = new TableRenderer(theme),
            [typeof(BlockquoteBlock)] = new BlockquoteRenderer(theme),
            [typeof(ListBlock)] = new ListRenderer(theme),
            [typeof(ThematicBreakBlock)] = new ThematicBreakRenderer(theme),
            [typeof(ImageBlock)] = new ImageRenderer(theme),
        };
    }

    /// <summary>
    /// Render blocks to FlowDocument.
    /// </summary>
    public FlowDocument Render(IEnumerable<Block> blocks)
    {
        var document = new FlowDocument
        {
            Background = new SolidColorBrush(_theme.BackgroundColor),
            Foreground = new SolidColorBrush(_theme.ForegroundColor),
            FontFamily = _theme.BodyFont,
            PagePadding = new Thickness(16),
        };

        foreach (var block in blocks)
        {
            if (_renderers.TryGetValue(block.GetType(), out var renderer))
            {
                var element = renderer.Render(block);
                document.Blocks.Add(element);
            }
        }

        return document;
    }
}

/// <summary>
/// Renders a specific block type to FlowDocument content.
/// </summary>
public interface IBlockRenderer
{
    System.Windows.Documents.Block Render(Block block);
}
```

**Renderer Implementation Example (Heading):**

```csharp
public sealed class HeadingRenderer : IBlockRenderer
{
    private readonly EditorTheme _theme;

    public HeadingRenderer(EditorTheme theme) => _theme = theme;

    public Block Render(Block block)
    {
        var heading = (HeadingBlock)block;
        var paragraph = new Paragraph
        {
            FontFamily = _theme.HeadingFont,
            Foreground = new SolidColorBrush(_theme.HeadingColor),
            FontSize = GetFontSize(heading.Level),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, _theme.HeadingMarginTop, 0, _theme.HeadingMarginBottom),
        };

        RenderInlines(paragraph, heading.Inlines);
        return paragraph;
    }

    private static double GetFontSize(int level) => level switch
    {
        1 => 28,
        2 => 24,
        3 => 20,
        4 => 18,
        5 => 16,
        6 => 14,
        _ => 14,
    };

    private void RenderInlines(Paragraph paragraph, List<Inline> inlines)
    {
        foreach (var inline in inlines)
        {
            paragraph.Inlines.Add(RenderInline(inline));
        }
    }

    private System.Windows.Documents.Inline RenderInline(Inline inline) => inline switch
    {
        TextInline t => new Run(t.Content),
        BoldInline b => new Bold(new Span(b.Children.Select(RenderInline).ToArray())),
        ItalicInline i => new Italic(new Span(i.Children.Select(RenderInline).ToArray())),
        CodeInline c => new Run(c.Code)
        {
            FontFamily = _theme.CodeFont,
            Background = new SolidColorBrush(_theme.CodeBackground),
        },
        LinkInline l => new Hyperlink(new Run(l.Children.FirstOrDefault() as TextInline?.Content ?? l.Url))
        {
            NavigateUri = new Uri(l.Url),
            Foreground = new SolidColorBrush(_theme.LinkColor),
        },
        _ => new Run(string.Empty),
    };
}
```

---

### 4. Theme System

**Note:** Theme classes are in the Wpf project because they use WPF-specific types (Color, FontFamily, Thickness).

```csharp
namespace WpfMarkdownEditor.Wpf.Theming;

/// <summary>
/// Defines visual styling for the Markdown editor.
/// </summary>
public sealed class EditorTheme
{
    public string Name { get; init; } = "Default";

    // Document colors
    public Color BackgroundColor { get; init; } = Colors.White;
    public Color ForegroundColor { get; init; } = Colors.Black;

    // Typography
    public FontFamily BodyFont { get; init; } = new("Segoe UI");
    public FontFamily HeadingFont { get; init; } = new("Segoe UI Semibold");
    public FontFamily CodeFont { get; init; } = new("Consolas");

    // Block-specific colors
    public Color HeadingColor { get; init; } = Color.FromRgb(0x1a, 0x1a, 0x1a);
    public Color CodeBackground { get; init; } = Color.FromRgb(0xf5, 0xf5, 0xf5);
    public Color CodeForeground { get; init; } = Color.FromRgb(0x24, 0x24, 0x24);
    public Color BlockquoteBorder { get; init; } = Color.FromRgb(0xdd, 0xdd, 0xdd);
    public Color BlockquoteBackground { get; init; } = Color.FromRgb(0xf9, 0xf9, 0xf9);
    public Color LinkColor { get; init; } = Color.FromRgb(0x00, 0x66, 0xcc);
    public Color TableHeaderBackground { get; init; } = Color.FromRgb(0xf0, 0xf0, 0xf0);
    public Color TableBorderColor { get; init; } = Color.FromRgb(0xdd, 0xdd, 0xdd);
    public Color ThematicBreakColor { get; init; } = Color.FromRgb(0xee, 0xee, 0xee);

    // Spacing (in pixels)
    public double ParagraphSpacing { get; init; } = 12;
    public double HeadingMarginTop { get; init; } = 24;
    public double HeadingMarginBottom { get; init; } = 8;
    public Thickness BlockquotePadding { get; init; } = new(16, 8, 16, 8);
    public Thickness BlockquoteBorderThickness { get; init; } = new(4, 0, 0, 0);

    // Built-in themes
    public static EditorTheme Light { get; } = new() { Name = "Light" };
    public static EditorTheme Dark { get; } = new()
    {
        Name = "Dark",
        BackgroundColor = Color.FromRgb(0x1e, 0x1e, 0x1e),
        ForegroundColor = Color.FromRgb(0xd4, 0xd4, 0xd4),
        HeadingColor = Color.FromRgb(0xff, 0xff, 0xff),
        CodeBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        CodeForeground = Color.FromRgb(0xd4, 0xd4, 0xd4),
        BlockquoteBackground = Color.FromRgb(0x28, 0x28, 0x28),
        LinkColor = Color.FromRgb(0x6c, 0xb6, 0xff),
        TableHeaderBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
    };
}
```

---

### 5. Main Control API

```csharp
namespace WpfMarkdownEditor.Wpf.Controls;

/// <summary>
/// Embeddable Markdown editor with side-by-side preview.
/// </summary>
public partial class MarkdownEditor : UserControl
{
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

    /// <summary>
    /// The Markdown content being edited. Two-way binding.
    /// </summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>
    /// Visual theme for the preview. One-way binding.
    /// </summary>
    public EditorTheme Theme
    {
        get => (EditorTheme)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>
    /// Show/hide the preview pane.
    /// </summary>
    public bool ShowPreview
    {
        get => (bool)GetValue(ShowPreviewProperty);
        set => SetValue(ShowPreviewProperty, value);
    }

    /// <summary>
    /// Width of the preview pane (GridLength for star/split sizing).
    /// </summary>
    public GridLength PreviewWidth
    {
        get => (GridLength)GetValue(PreviewWidthProperty);
        set => SetValue(PreviewWidthProperty, value);
    }

    /// <summary>
    /// Fired when Markdown content changes.
    /// </summary>
    public event EventHandler<MarkdownChangedEventArgs>? MarkdownChanged;

    #endregion

    #region Methods

    /// <summary>
    /// Load Markdown from a file.
    /// </summary>
    public void LoadFile(string path) => Markdown = File.ReadAllText(path);

    /// <summary>
    /// Save Markdown to a file.
    /// </summary>
    public async Task SaveFileAsync(string path) =>
        await File.WriteAllTextAsync(path, Markdown);

    /// <summary>
    /// Apply a new theme at runtime.
    /// </summary>
    public void ApplyTheme(EditorTheme theme) => Theme = theme;

    /// <summary>
    /// Focus the editor TextBox.
    /// </summary>
    public void FocusEditor() => EditorTextBox.Focus();

    #endregion
}

public sealed class MarkdownChangedEventArgs : EventArgs
{
    public string OldMarkdown { get; init; } = string.Empty;
    public string NewMarkdown { get; init; } = string.Empty;
}
```

**XAML Template:**

```xaml
<UserControl x:Class="WpfMarkdownEditor.Wpf.Controls.MarkdownEditor"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="1*" />
            <ColumnDefinition Width="Auto" />
            <ColumnDefinition Width="{Binding PreviewWidth, RelativeSource={RelativeSource AncestorType=UserControl}}" />
        </Grid.ColumnDefinitions>

        <!-- Editor Pane -->
        <TextBox x:Name="EditorTextBox"
                 Grid.Column="0"
                 AcceptsReturn="True"
                 AcceptsTab="True"
                 VerticalScrollBarVisibility="Auto"
                 HorizontalScrollBarVisibility="Auto"
                 Text="{Binding Markdown, RelativeSource={RelativeSource AncestorType=UserControl}, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                 TextChanged="OnEditorTextChanged" />

        <!-- Splitter -->
        <GridSplitter Grid.Column="1"
                      Width="5"
                      HorizontalAlignment="Center"
                      VerticalAlignment="Stretch"
                      Background="{DynamicResource SplitterBrush}" />

        <!-- Preview Pane -->
        <FlowDocumentReader x:Name="PreviewReader"
                            Grid.Column="2"
                            ViewingMode="Scroll"
                            Visibility="{Binding ShowPreview, RelativeSource={RelativeSource AncestorType=UserControl}, Converter={StaticResource BoolToVisibilityConverter}}" />
    </Grid>
</UserControl>
```

**Value Converter:**

```csharp
namespace WpfMarkdownEditor.Wpf.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}
```

---

### 6. Performance Strategy

**Real-time Update Pipeline:**

```
User types → TextChanged event
         → Stop debounce timer (cancel pending parse)
         → Start debounce timer (100ms delay)
         → On timer tick: Task.Run(ParseAndRender)
         → Parse on background thread
         → Render FlowDocument on background thread
         → Dispatcher.Invoke to update PreviewReader.Document
```

**Implementation:**

```csharp
public partial class MarkdownEditor
{
    private readonly DispatcherTimer _debounceTimer;
    private readonly MarkdownParser _parser = new();
    private CancellationTokenSource? _cts;
    private FlowDocumentRenderer? _renderer;

    public MarkdownEditor()
    {
        InitializeComponent();
        _debounceTimer = new(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _debounceTimer.Tick += OnDebounceTick;
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        _debounceTimer.Stop();
        _cts?.Cancel();
        _debounceTimer.Start();
    }

    private async void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _cts = new CancellationTokenSource();

        try
        {
            var markdown = Markdown;
            var theme = Theme;

            var document = await Task.Run(() =>
            {
                var blocks = _parser.Parse(markdown);
                var renderer = new FlowDocumentRenderer(theme);
                return renderer.Render(blocks);
            }, _cts.Token);

            PreviewReader.Document = document;
        }
        catch (OperationCanceledException)
        {
            // Newer parse superseded this one
        }
    }
}
```

**Performance Targets:**

| Document Size | Target Update Time |
|---------------|-------------------|
| < 1KB | < 16ms (single frame) |
| 1KB - 10KB | < 50ms |
| 10KB - 100KB | < 200ms |
| > 100KB | Document not suited for real-time; warn user |

---

### 7. Image Handling

**Strategies:**

1. **Local images** (`![](image.png)`): Load from file path relative to optional `BaseDirectory` property
2. **Embedded images** (`![](data:image/png;base64,...)`): Decode and display
3. **Remote images** (`![](https://...`): Async download with placeholder, cache to temp

**Implementation:**

```csharp
public sealed class ImageRenderer : IBlockRenderer
{
    public Block Render(Block block)
    {
        var image = (ImageBlock)block;
        var container = new BlockUIContainer();

        var wpfImage = new System.Windows.Controls.Image
        {
            Stretch = Stretch.Uniform,
            MaxWidth = 800,
            MaxHeight = 600,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        if (image.Url.StartsWith("http"))
        {
            // Async load with placeholder
            LoadRemoteImageAsync(wpfImage, image.Url);
        }
        else if (image.Url.StartsWith("data:"))
        {
            // Base64 embedded
            LoadEmbeddedImage(wpfImage, image.Url);
        }
        else
        {
            // Local file
            LoadLocalImage(wpfImage, image.Url);
        }

        container.Child = wpfImage;
        return container;
    }

    private async void LoadRemoteImageAsync(System.Windows.Controls.Image image, string url)
    {
        // Show placeholder
        image.Source = CreatePlaceholder();

        try
        {
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                image.Source = LoadFromBytes(bytes);
            });
        }
        catch
        {
            image.Source = CreateErrorPlaceholder();
        }
    }
}
```

---

### 8. Code Block Syntax Highlighting

**Simple approach** (no full lexer):

```csharp
public sealed class CodeBlockRenderer : IBlockRenderer
{
    private static readonly Dictionary<string, SolidColorBrush[]> LanguageColors = new()
    {
        ["csharp"] = [/* keywords, strings, comments, numbers */],
        ["javascript"] = [/* ... */],
        ["python"] = [/* ... */],
        // Common languages
    };

    public Block Render(Block block)
    {
        var code = (CodeBlock)block;
        var paragraph = new Paragraph
        {
            FontFamily = _theme.CodeFont,
            Background = new SolidColorBrush(_theme.CodeBackground),
            Foreground = new SolidColorBrush(_theme.CodeForeground),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 8, 0, 8),
        };

        // Simple keyword highlighting
        if (code.Language is not null &&
            LanguageColors.TryGetValue(code.Language, out var colors))
        {
            RenderHighlighted(paragraph, code.Code, code.Language);
        }
        else
        {
            paragraph.Inlines.Add(new Run(code.Code));
        }

        return paragraph;
    }
}
```

---

## Testing Strategy

### Unit Tests (Core)

| Test Category | Examples |
|---------------|----------|
| Heading parsing | `# H1`, `## H2`, up to `###### H6` |
| Paragraph | Plain text, multi-line, trailing spaces |
| Code blocks | Fenced with language, indented, nested in list |
| Tables | Simple, aligned columns, missing cells |
| Lists | Ordered, unordered, nested, mixed |
| Blockquotes | Simple, nested, with other blocks inside |
| Inlines | Bold, italic, bold+italic, code, links, images |
| Edge cases | Empty document, only whitespace, malformed MD |

### Unit Tests (WPF)

| Test Category | Examples |
|---------------|----------|
| Renderer output | Verify correct FlowDocument elements generated |
| Theme application | Verify colors/fonts applied correctly |
| Control binding | Verify DP binding works as expected |

### Performance Tests

```csharp
[Fact]
public async Task ParseAndRender_1KB_Under50ms()
{
    var markdown = GenerateMarkdown(1024);
    var parser = new MarkdownParser();
    var renderer = new FlowDocumentRenderer(EditorTheme.Light);

    var sw = Stopwatch.StartNew();
    var blocks = parser.Parse(markdown);
    var doc = renderer.Render(blocks);
    sw.Stop();

    Assert.True(sw.ElapsedMilliseconds < 50);
}
```

---

## Dependencies

```
WpfMarkdownEditor.Core/
  - .NET 8
  - No external packages

WpfMarkdownEditor.Wpf/
  - .NET 8
  - PresentationCore
  - PresentationFramework
  - WindowsBase
  - No external packages
```

**Optional future enhancements:**
- `Markdig` - Drop-in parser replacement for extended MD features
- `ColorCode.NET` - Advanced syntax highlighting
- `AvalonEdit` - Enhanced editor with line numbers, folding

---

## Future Considerations

1. **Undo/Redo integration** - Expose editor's undo stack
2. **Find/Replace** - Built-in search in preview
3. **Export** - PDF, HTML export from FlowDocument
4. **Custom blocks** - Allow registering custom block renderers
5. **Spell check** - Integrate WPF spell checker
6. **Math support** - LaTeX rendering via custom block type

---

## Success Criteria

- [ ] Parse all standard Markdown elements correctly
- [ ] Preview updates in <50ms for typical documents
- [ ] Light and dark themes render correctly
- [ ] Control embeds cleanly in sample WPF application
- [ ] Zero external dependencies
- [ ] 80%+ test coverage on Core library
