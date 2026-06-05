# Format Menu Optimization Design

## Summary

Optimize the sample application's top Format menu around immediately usable inline Markdown and HTML formatting operations. The first version should match the screenshot where the behavior is stable, while keeping ambiguous submenu items out of the UI until link and image context detection is designed.

The product direction is "usable first", not screenshot completeness. Deferred submenu items should not appear as disabled placeholders in the first version.

## Current State

The sample app currently has a top Format menu with:

- Bold
- Italic
- Strikethrough
- Inline Code

The handlers live in `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs` and call `MarkdownEditor.WrapSelection(...)`:

- Bold: `WrapSelection("**", "**")`
- Italic: `WrapSelection("*", "*")`
- Strikethrough: `WrapSelection("~~", "~~")`
- Inline Code: `WrapSelection("`", "`")`

Related behavior already exists outside Format:

- Link insertion is currently in the Insert menu through `OnLink`.
- The editor exposes `WrapSelection`, `InsertText`, and the newer pure text-edit application path used by paragraph operations.
- The current menu is button-based, not WPF command-based.

## Goals

- Expand the Format menu with stable inline formatting actions from the screenshot.
- Keep first-version menu items genuinely executable.
- Avoid adding more formatting algorithms directly to `MainWindow`.
- Reuse existing `WrapSelection` where it is sufficient.
- Add a focused pure helper for clear-style behavior, where wrapper removal needs deterministic text logic.
- Keep localization synchronized in English and Chinese.
- Keep deferred screenshot submenus out of the first-version UI.

## Non-goals

- Do not implement the Link Operations submenu in the first version.
- Do not implement the Image submenu in the first version.
- Do not add disabled placeholders for deferred submenus.
- Do not add a Format menu ViewModel for this small scope.
- Do not add new dependencies.
- Do not implement rich Markdown AST inline editing in this pass.
- Do not remove the existing Insert menu Link entry; Format can duplicate the Link entry while preserving the old path.

## First-Version Menu Scope

The first version should include these items:

| Menu item | Shortcut label | Behavior |
| --- | --- | --- |
| Bold | `Ctrl+B` | Wrap selection in `**`, or insert selected default text wrapped in `**`. |
| Italic | `Ctrl+I` | Wrap selection in `*`, or insert selected default text wrapped in `*`. |
| Underline | `Ctrl+U` | Wrap selection in `<u>...</u>`, or insert selected default text wrapped in `<u>...</u>`. |
| Inline Code | `Ctrl+Shift+`` | Wrap selection in backticks, or insert selected default text wrapped in backticks. |
| Strikethrough | `Alt+Shift+5` | Wrap selection in `~~`, or insert selected default text wrapped in `~~`. |
| Comment | none | Wrap selection in `<!-- ... -->`, or insert selected default text wrapped in an HTML comment. |
| Hyperlink | `Ctrl+K` | Reuse the existing link insertion behavior: `WrapSelection("[", "](url)")`. |
| Clear Style | `Ctrl+\` | Remove supported inline wrappers from the current selection. If there is no selection, leave the document unchanged and focus the editor. |

`Hyperlink` should be duplicated into Format because it is an inline formatting operation in the screenshot. Keep the existing Insert menu Link entry during this pass so users do not lose the current path.

`Underline` uses HTML `<u>` because standard Markdown does not define underline syntax. The renderer already supports an HTML subset; if underline rendering is incomplete, that is a renderer follow-up, not a reason to invent custom Markdown syntax here.

## Deferred Items

| Screenshot item | Phase | Reason |
| --- | --- | --- |
| Link Operations submenu | Phase 2 | Requires detecting whether the caret or selection is inside a Markdown link and defining edit/open/copy/remove semantics. |
| Image submenu | Phase 2 | Requires detecting image inline/block context, path resolution, open/replace/copy behavior, and interaction with existing paste-image behavior. |
| Smart Clear Style with no selection | Phase 2 | Requires current inline span detection to avoid deleting nearby Markdown syntax incorrectly. |
| Toggle active formatting state | Phase 2 | Requires command state detection and menu checked/disabled states rather than simple buttons. |

## Architecture

### MainWindow.xaml

`MainWindow.xaml` remains the owner of the top menu layout. The Format popup should be expanded in place and continue using existing menu styles.

Responsibilities:

- Render the first-version Format menu groups.
- Show shortcut labels in `Tag`.
- Keep deferred submenus out of the UI.
- Duplicate Hyperlink into Format without removing its existing Insert menu entry.

`MainWindow.xaml` should not contain formatting algorithms.

### MainWindow.EditorUi.cs

`MainWindow.EditorUi.cs` should remain a thin bridge between menu clicks and editor operations.

Allowed responsibilities:

- Call existing `Editor.WrapSelection(...)` for simple wrappers.
- Call a focused `MarkdownEditor` method for clear style.
- Reuse the existing `OnLink` handler for Hyperlink.

Avoid adding inline parsing, wrapper cleanup, link detection, or image context logic here.

### MarkdownEditor

`MarkdownEditor` remains the reusable text editing surface.

First-version responsibilities:

- Continue exposing `WrapSelection` for simple wrapper actions.
- Add a narrow clear-style method named `ClearInlineStyle()`.
- Apply clear-style text operations inside one undo unit.
- Keep `Markdown` synchronized with `TextBox.Text` after programmatic edits.
- Preserve caret and selection behavior after text edits.

### Inline Format Operations Helper

Add a focused, stateless helper when implementation begins:

`src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs`

The helper should be pure and testable:

- Input: source text, selection start, selection length.
- Output: `TextEditOperation?`.
- Return `null` when there is no selection.
- No WPF controls.
- No clipboard.
- No localization.
- No sample app references.

Candidate operation:

- `ClearInlineStyle(text, selectionStart, selectionLength)`

Supported first-version cleanup rules:

- `**text**` becomes `text`.
- `*text*` becomes `text`.
- `~~text~~` becomes `text`.
- `` `text` `` becomes `text`.
- `<u>text</u>` becomes `text`.
- `<!-- text -->` becomes `text`.
- `[text](url)` becomes `text`.

The cleanup should operate on the selected text only. It should not scan outside the selection in the first version.

## Data Flow

Simple wrapper flow:

```text
Top Format menu item
  -> MainWindow.EditorUi thin handler
  -> MarkdownEditor.WrapSelection(before, after)
  -> TextBox text updates
  -> Markdown property updates through existing editor binding/event flow
  -> preview debounce and dirty-state handling
```

Clear-style flow:

```text
Top Format menu Clear Style
  -> MainWindow.EditorUi thin handler
  -> MarkdownEditor.ClearInlineStyle()
  -> MarkdownInlineFormatOperations.ClearInlineStyle(...)
  -> TextEditOperation
  -> MarkdownEditor applies text edit
  -> Markdown property updates
  -> preview debounce and dirty-state handling
```

Hyperlink flow:

```text
Top Format menu Hyperlink
  -> existing OnLink handler
  -> Editor.WrapSelection("[", "](url)")
```

This mirrors the existing Insert menu Link behavior and avoids inventing a new link workflow.

## Localization

Add or reuse localization keys in:

- `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`

Existing keys already cover:

- Bold
- Italic
- Strikethrough
- Inline Code
- Link

Add these first-version keys:

| Key | English | Chinese |
| --- | --- | --- |
| `MainWindow.Underline` | Underline | 下划线 |
| `MainWindow.Comment` | Comment | 注释 |
| `MainWindow.Hyperlink` | Hyperlink | 超链接 |
| `MainWindow.ClearStyle` | Clear Style | 清除样式 |

`Hyperlink` intentionally gets its own top Format menu label even though the existing Insert menu uses `MainWindow.Link`.

## Testing Strategy

### Pure Operation Tests

Add tests for `MarkdownInlineFormatOperations`:

- `ClearInlineStyle_NoSelection_ReturnsNull`
- `ClearInlineStyle_BoldSelection_RemovesWrapper`
- `ClearInlineStyle_ItalicSelection_RemovesWrapper`
- `ClearInlineStyle_StrikethroughSelection_RemovesWrapper`
- `ClearInlineStyle_CodeSelection_RemovesWrapper`
- `ClearInlineStyle_UnderlineSelection_RemovesWrapper`
- `ClearInlineStyle_CommentSelection_RemovesWrapper`
- `ClearInlineStyle_LinkSelection_KeepsLinkText`
- `ClearInlineStyle_MixedSelection_RemovesSupportedWrappers`

### WPF Integration Tests

Add focused WPF tests for editor application behavior:

- `ClearInlineStyle_Selection_UpdatesMarkdown`
- `ClearInlineStyle_NoSelection_DoesNotChangeText`
- `WrapSelection_UnderlineMarkers_UsesHtmlUnderline`

### Localization Tests

Add or update tests that verify English and Chinese keys exist for new Format labels.

### Menu Shape Tests

Add a sample app menu test for first-version menu shape:

- Bold, Italic, Underline, Inline Code present.
- Strikethrough and Comment present.
- Hyperlink present.
- Clear Style present.
- Existing Insert menu Link entry remains present.
- Link Operations and Image submenus absent.

## Acceptance Criteria

- Format menu exposes only approved first-version items.
- Bold, Italic, Underline, Inline Code, Strikethrough, Comment, Hyperlink, and Clear Style are available.
- Existing Insert menu Link entry remains available.
- Deferred screenshot submenus are not shown in the first-version UI.
- `MainWindow.EditorUi.cs` remains a thin bridge and does not gain inline parsing algorithms.
- New clear-style algorithm lives in a focused WPF-layer helper with pure operation tests.
- Clear Style with no selection leaves the document unchanged.
- Text edits preserve one undo unit where operations rewrite selected text.
- `Markdown` stays synchronized with the editor text.
- English and Chinese localization keys are complete.
- Relevant pure operation tests and WPF integration tests pass.
