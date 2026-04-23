# Markdown Viewer

A modern, zero-dependency Markdown editor & viewer for WPF with real-time preview, multi-engine translation, and 6 built-in themes. Built on .NET 8.

[中文文档](README.zh-CN.md)

## Features

- **Real-time Preview** — Side-by-side editing with debounced rendering (~100ms)
- **Multi-engine Translation** — Translate documents via Baidu or OpenAI-compatible APIs (Qwen, DeepSeek, etc.), preview-only mode preserves original content
- **6 Built-in Themes** — GitHub, GitHub Dark, Claude, Claude Dark, Light, Dark
- **Smart Editing** — Auto-continuation for lists, prefix toggling, selection wrapping
- **Formatting Toolbar** — Headings, bold, italic, code, links, tables, and more
- **Sidebar** — File history & document outline (TOC) with animated toggle
- **Syntax Highlighting** — Code blocks with C#, JavaScript/TypeScript, Python, JSON, SQL, Bash support
- **Zero Dependencies** — Pure WPF, no NuGet packages required

## Screenshots

### GitHub Light
![GitHub Light Theme](sources/github_light_theme.png)

### GitHub Dark
![GitHub Dark Theme](sources/github_dark_theme.png)

### Claude
![Claude Light Theme](sources/claude_light_theme.png)

## Getting Started

### Requirements

- .NET 8.0 SDK
- Windows 10/11

### Install

Add the NuGet packages to your WPF project:

```xml
<ItemGroup>
  <PackageReference Include="WpfMarkdownEditor.Core" Version="0.1.0" />
  <PackageReference Include="WpfMarkdownEditor.Wpf" Version="0.1.0" />
</ItemGroup>
```

Or reference the projects directly:

```xml
<ItemGroup>
  <ProjectReference Include="path\to\WpfMarkdownEditor.Core\WpfMarkdownEditor.Core.csproj" />
  <ProjectReference Include="path\to\WpfMarkdownEditor.Wpf\WpfMarkdownEditor.Wpf.csproj" />
</ItemGroup>
```

### Quick Start

```xml
<Window xmlns:ctrl="clr-namespace:WpfMarkdownEditor.Wpf.Controls;assembly=WpfMarkdownEditor.Wpf">
    <ctrl:MarkdownEditor x:Name="Editor"
                         Markdown="# Hello Markdown"
                         ShowPreview="True" />
</Window>
```

```csharp
// Load a file
Editor.LoadFile("README.md");

// Apply a theme
Editor.ApplyTheme(EditorTheme.GitHub);

// Listen for changes
Editor.MarkdownChanged += (s, e) =>
{
    Console.WriteLine($"Content changed ({e.NewMarkdown.Length} chars)");
};
```

## Translation

Translate markdown documents while preserving all formatting (headings, lists, tables, code blocks, inline markers). Translation renders in the **preview pane only** — the editor stays untouched.

### Supported Providers

| Provider | Service |
|----------|---------|
| **Baidu Translate** | 百度翻译 API |
| **OpenAI Compatible** | Qwen, DeepSeek, Zhipu, OpenAI, any Chat Completions API |

### Supported Languages

English, Chinese (中文), Japanese (日本語), Korean (한국어)

### How It Works

1. **Template-based extraction** — Parses markdown into segments, extracts only translatable text, replaces inline markers (bold, italic, code, links) with ASCII tokens
2. **Translate** — Sends clean plain text to the translation API
3. **Reconstruct** — Rebuilds markdown from the template with translated text, restoring all formatting
4. **Preview** — Renders translated content in the preview pane; original editor content unchanged

```csharp
// Translate and show in preview
var service = new TranslationService(provider);
var result = await service.TranslateMarkdownAsync(
    Editor.Markdown, TranslationLanguage.Chinese, progress, ct);
Editor.RenderTranslatedPreview(result.TranslatedText);

// Clear translation, revert to original
Editor.ClearTranslatedPreview();
```

## API Reference

### Dependency Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Markdown` | `string` | `""` | Markdown content (two-way binding) |
| `Theme` | `EditorTheme` | `Light` | Current editor theme |
| `ShowPreview` | `bool` | `true` | Show/hide the preview pane |
| `PreviewWidth` | `GridLength` | `1*` | Preview pane width |

### Methods

| Method | Description |
|--------|-------------|
| `LoadFile(string path)` | Load markdown from a file |
| `SaveFileAsync(string path)` | Save markdown to a file |
| `ApplyTheme(EditorTheme theme)` | Apply a theme |
| `FocusEditor()` | Focus the editor text box |
| `WrapSelection(string before, string after)` | Wrap selected text with markers |
| `InsertText(string text)` | Insert text at cursor position |
| `ToggleLinePrefix(string prefix)` | Toggle heading/quote/list prefixes |
| `RenderTranslatedPreview(string md)` | Show translated markdown in preview |
| `ClearTranslatedPreview()` | Revert preview to editor content |

### Events

| Event | Description |
|-------|-------------|
| `MarkdownChanged` | Fired when markdown content changes |

## Themes

Six built-in themes, all customizable:

```csharp
// Built-in themes
Editor.ApplyTheme(EditorTheme.GitHub);
Editor.ApplyTheme(EditorTheme.GitHubDark);
Editor.ApplyTheme(EditorTheme.Claude);
Editor.ApplyTheme(EditorTheme.ClaudeDark);
Editor.ApplyTheme(EditorTheme.Light);
Editor.ApplyTheme(EditorTheme.Dark);

// Custom theme
var custom = new EditorTheme
{
    Name = "My Theme",
    BaseFontSize = 13,          // base body font size (headings scale proportionally)
    LineHeight = 22,            // line height in device-independent pixels (NaN = auto)
    BackgroundColor = Colors.White,
    ForegroundColor = Colors.Black,
    BodyFont = new FontFamily("Segoe UI Variable, Segoe UI"),
    HeadingFont = new FontFamily("Segoe UI Variable, Segoe UI"),
    CodeFont = new FontFamily("Cascadia Mono, Consolas"),
    LinkColor = Colors.Blue,
    ParagraphSpacing = 10,
    HeadingMarginTop = 14,
    HeadingMarginBottom = 4,
    // ... see EditorTheme for all properties
};
Editor.ApplyTheme(custom);
```

### Typography Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `BaseFontSize` | `double` | `14` | Body font size; headings scale relative to this value |
| `LineHeight` | `double` | `NaN` | Line height in DIPs; `NaN` = WPF automatic |
| `BodyFont` | `FontFamily` | `"Segoe UI"` | Font used for paragraphs and lists |
| `HeadingFont` | `FontFamily` | `"Segoe UI Semibold"` | Font used for headings |
| `CodeFont` | `FontFamily` | `"Consolas"` | Font used for code blocks and inline code |
| `ParagraphSpacing` | `double` | `12` | Bottom margin between paragraphs (DIPs) |
| `HeadingMarginTop` | `double` | `24` | Top margin before headings (DIPs) |
| `HeadingMarginBottom` | `double` | `8` | Bottom margin after headings (DIPs) |

### Using FlowDocumentRenderer Directly

Use `FlowDocumentRenderer` standalone to render markdown into a `FlowDocument` — useful for read-only chat bubbles, tooltips, or any custom WPF control:

```csharp
var parser = new MarkdownParser();
var renderer = new FlowDocumentRenderer(EditorTheme.ClaudeDark);

var blocks = parser.Parse(markdownText);
var document = renderer.Render(blocks);

// Attach to any FlowDocumentScrollViewer / RichTextBox
myFlowDocumentScrollViewer.Document = document;
```

You can also expose theme as a `DependencyProperty` on your control to allow XAML configuration without modifying the library:

```xml
<local:MyMarkdownControl
    Markdown="{Binding Text}"
    Theme="{x:Static themes:AppThemes.ClaudeCode}" />
```

## Smart Editing

- **List auto-continuation** — Press Enter in a list to auto-insert the next marker
- **Numbered list increment** — `1.` → `2.` → `3.` automatically
- **Tab/Shift+Tab** — Indent/outdent list items
- **Empty list cleanup** — Press Enter on an empty item to remove the marker
- **Paste image support** — Paste clipboard images or drag-drop files to insert markdown image syntax

## Syntax Highlighting

| Language | Aliases |
|----------|---------|
| C# | `csharp`, `cs` |
| JavaScript / TypeScript | `javascript`, `js`, `typescript`, `ts`, `jsx`, `tsx` |
| Python | `python`, `py` |
| JSON | `json`, `jsonc` |
| SQL | `sql`, `postgres`, `mysql`, `sqlite` |
| Shell | `bash`, `sh`, `shell`, `zsh` |

## Converter

`WpfMarkdownEditor.Converters` implements the [markitdown-csharp](https://github.com/WenElevating/markitdown-csharp) `IConverter` interface, converting Markdown to WPF FlowDocument.

```csharp
using MarkItDown.Core;
using WpfMarkdownEditor.Converters;
using WpfMarkdownEditor.Wpf.Theming;

// Create converter with custom theme
var converter = new MarkdownToFlowDocumentConverter(EditorTheme.GitHub);

// Direct FlowDocument output (no serialization)
var document = converter.ConvertToFlowDocument("# Hello World");

// XAML string output (via IConverter interface)
var result = await converter.ConvertAsync(
    new DocumentConversionRequest { FilePath = "README.md" });
Console.WriteLine(result.Kind);     // "FlowDocument"
Console.WriteLine(result.Markdown);  // XAML string
```

## Project Structure

```
src/
  WpfMarkdownEditor.Core/         — Markdown parser, AST, translation extraction
  WpfMarkdownEditor.Wpf/          — WPF control library, rendering, themes, translation providers
  WpfMarkdownEditor.Converters/   — MarkItDown IConverter: Markdown → FlowDocument
samples/
  WpfMarkdownEditor.Sample/       — Demo application
tests/
  WpfMarkdownEditor.Core.Tests/     — Core unit tests
  WpfMarkdownEditor.Wpf.Tests/      — WPF integration tests
  WpfMarkdownEditor.Converters.Tests/ — Converter unit tests (19 tests)
```

## Building

```bash
git clone https://github.com/WenElevating/wpf-markdown-viewer.git
cd wpf-markdown-viewer
dotnet build
dotnet run --project samples/WpfMarkdownEditor.Sample
```

## Running Tests

```bash
dotnet test
```

## Acknowledgments

- [markitdown](https://github.com/microsoft/markitdown) — Microsoft's markdown conversion library
- [oh-my-claudecode](https://github.com/Yeachan-Heo/oh-my-claudecode) — Claude Code enhancement plugin

## License

MIT
