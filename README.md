# WPF Markdown Editor

A modern, zero-dependency Markdown editor control for WPF with real-time preview, built on .NET 8.

[中文文档](README.zh-CN.md)

## Features

- **Real-time Preview** — Side-by-side editing with debounced rendering (~100ms)
- **6 Built-in Themes** — GitHub, GitHub Dark, Claude, Claude Dark, Light, Dark
- **Smart Editing** — Auto-continuation for lists, prefix toggling, selection wrapping
- **Formatting Toolbar** — Headings, bold, italic, code, links, tables, and more
- **Sidebar** — File history & document outline (TOC) with animated toggle
- **Syntax Highlighting** — Code blocks with C#, JavaScript, Python support
- **Zero Dependencies** — Pure WPF, no NuGet packages required

## Screenshots

> Coming soon

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
    BackgroundColor = Colors.White,
    ForegroundColor = Colors.Black,
    LinkColor = Colors.Blue,
    // ... see EditorTheme for all properties
};
Editor.ApplyTheme(custom);
```

## Smart Editing

The editor provides intelligent Markdown editing:

- **List auto-continuation** — Press Enter in a list to auto-insert the next marker
- **Numbered list increment** — `1.` → `2.` → `3.` automatically
- **Tab/Shift+Tab** — Indent/outdent list items
- **Empty list cleanup** — Press Enter on an empty item to remove the marker

## Formatting Toolbar

The sample app includes a complete toolbar:

| Category | Actions |
|----------|---------|
| File | Open, Save |
| Headings | H1, H2, H3 |
| Formatting | Bold, Italic, Strikethrough, Inline Code |
| Insert | Link, Blockquote, Bullet List, Numbered List, Code Block, Table, Horizontal Rule |
| Theme | Dropdown picker with all 6 themes |
| Sidebar | Toggle animated sidebar |

## Sidebar

The sidebar provides two tabs with animated show/hide:

- **History** — Recently opened files with timestamps, click to reopen
- **Outline** — Document heading tree extracted from the current markdown

## Project Structure

```
src/
  WpfMarkdownEditor.Core/    — Markdown parser & AST model
  WpfMarkdownEditor.Wpf/     — WPF editor control & rendering
samples/
  WpfMarkdownEditor.Sample/  — Demo application
tests/
  WpfMarkdownEditor.Core.Tests/  — 145 unit tests
  WpfMarkdownEditor.Wpf.Tests/   — WPF test project
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

## License

MIT
