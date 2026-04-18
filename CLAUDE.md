# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
dotnet build                        # Build solution
dotnet test                         # Run all tests (223 tests)
dotnet test tests/WpfMarkdownEditor.Core.Tests   # Core tests only
dotnet test --filter "FullyQualifiedName~MarkdownParserTests"  # Single test class
dotnet test --filter "TestMethodName"             # Single test
dotnet publish samples/WpfMarkdownEditor.Sample -c Release  # Publish sample app
build-installer.bat                 # One-click: publish + Inno Setup installer
```

## Architecture

Two-library split with sample app:

- **WpfMarkdownEditor.Core** (net8.0, no WPF deps) — Markdown parser, AST models (`Block`, `Inline`), translation segment extraction
- **WpfMarkdownEditor.Wpf** (net8.0-windows) — WPF control library: renderer, themes, syntax highlighting, translation providers
- **WpfMarkdownEditor.Sample** — Demo app with toolbar, sidebar, file history, outline

### Rendering Pipeline

```
TextBox input → DebounceTimer (100ms) → MarkdownParser → Block AST → FlowDocumentRenderer → FlowDocument (Preview)
```

- Background thread parsing with `CancellationTokenSource` to cancel stale renders
- Strategy pattern: one `IRenderer` implementation per block type in `Rendering/`
- `_translatedMarkdown` field overrides preview content during translation mode; auto-clears on edit

### Translation Pipeline

```
Markdown → MarkdownSegmentExtractor.Extract() → plainText + template + inlineTokens
  → TranslationService.TranslateMarkdownAsync() → provider.TranslateAsync(plainText)
  → MarkdownSegmentExtractor.Reconstruct() → translated markdown
```

- Template-based extraction: strips all markdown syntax, sends only plain text with ASCII tokens (XBS/XBE for bold, XIS/XIE for italic, XCS/XCE for code, XLS/XLE for links)
- Code blocks are preserved (never sent to translation API)
- Multi-provider: Baidu (traditional MT) and OpenAI-compatible (Qwen, DeepSeek, etc.)
- Credentials encrypted via DPAPI in `TranslationSettingsService`

### Key Design Decisions

- Zero external NuGet dependencies (pure .NET 8 + WPF)
- Immutable `EditorTheme` with 23 customizable properties, 6 built-in themes
- `MarkdownEditor` is a `UserControl` with DP `Markdown` property — embeddable in any WPF app
- Smart editing: auto-continue lists, tab indentation, prefix toggling, paste image support
- Dialog windows use `WindowStyle="None"` + `AllowsTransparency="True"` — requires explicit `PreviewKeyDown` for Ctrl+V paste handling

## Project References

```
Sample → WpfMarkdownEditor.Wpf → WpfMarkdownEditor.Core
Core.Tests → WpfMarkdownEditor.Core
Wpf.Tests → WpfMarkdownEditor.Wpf → WpfMarkdownEditor.Core
```
