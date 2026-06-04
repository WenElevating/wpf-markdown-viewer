# Paragraph Menu Optimization Design

## Summary

Optimize the sample application's top Paragraph menu around immediately usable Markdown block operations. The first version should expose stable commands that can be implemented with deterministic text edits and the editor's existing insertion/toggle primitives. More complex screenshot items should be deferred until their syntax, parser behavior, rendering behavior, and selection semantics are designed.

The product direction is "usable first", not screenshot completeness. Deferred items should not appear as disabled placeholders in the first version.

## Current State

The sample app currently has a top Paragraph menu with:

- Heading 1
- Heading 2
- Heading 3
- Blockquote
- Ordered List
- Bullet List

The handlers live in `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs` and call methods on `MarkdownEditor`:

- `ToggleLinePrefix("#")`, `ToggleLinePrefix("##")`, `ToggleLinePrefix("###")`
- `ToggleLinePrefix(">")`
- `ToggleLinePrefix("1.")`
- `ToggleLinePrefix("-")`

Related editor primitives already exist:

- `MarkdownEditor.ToggleLinePrefix(string prefix)`
- `MarkdownEditor.WrapSelection(string before, string after)`
- `MarkdownEditor.InsertText(string text)`
- `EditorTextOperations.InsertText(...)`
- smart Enter/Tab behavior for simple list continuation and indentation

Related menu features already exist outside Paragraph:

- Code block is currently in the Insert menu.
- Table insertion is currently in the Insert menu through `TableInsertDialog` and `MarkdownInsertService.GenerateTable`.
- Horizontal rule is currently in the Insert menu.

## Goals

- Expand the Paragraph menu with stable block-level Markdown operations.
- Keep first-version menu items genuinely executable.
- Avoid putting more text-editing algorithms directly in `MainWindow`.
- Reuse existing editor primitives where they are already sufficient.
- Add focused helper methods only where existing primitives are too broad.
- Keep localization synchronized in English and Chinese.
- Keep deferred screenshot items out of the first-version UI.

## Non-goals

- Do not implement all screenshot items in the first version.
- Do not add disabled placeholders for deferred items.
- Do not introduce a Paragraph menu ViewModel for this small scope.
- Do not add new dependencies.
- Do not add math rendering, admonition rendering, footnote rendering, or TOC rendering as part of the first version.
- Do not remove existing Insert menu entries in the first version; shared block insertions can be duplicated in Paragraph while preserving the old path.

## First-Version Menu Scope

The first version should include these items:

| Menu item | Shortcut label | Behavior |
| --- | --- | --- |
| Heading 1 | `Ctrl+1` | Set current line or selected lines to `#`. |
| Heading 2 | `Ctrl+2` | Set current line or selected lines to `##`. |
| Heading 3 | `Ctrl+3` | Set current line or selected lines to `###`. |
| Heading 4 | `Ctrl+4` | Set current line or selected lines to `####`. |
| Heading 5 | `Ctrl+5` | Set current line or selected lines to `#####`. |
| Heading 6 | `Ctrl+6` | Set current line or selected lines to `######`. |
| Paragraph | `Ctrl+0` | Remove supported block prefix from current line or selected lines. |
| Blockquote | `Ctrl+Shift+Q` | Toggle `>` prefix on current line or selected lines. |
| Ordered List | `Ctrl+Shift+[` | Toggle ordered-list marker on current line or selected lines. |
| Bullet List | `Ctrl+Shift+]` | Toggle unordered-list marker on current line or selected lines. |
| Code Block | `Ctrl+Shift+K` | Wrap selection in fenced code block, or insert a starter fenced code block. |
| Table | none | Open existing table insertion dialog. |
| Horizontal Rule | none | Insert `---` as a block with safe blank-line boundaries. |
| Insert Paragraph Above | none | Insert a blank paragraph above the current line or selected line range and place the caret in the inserted paragraph. |
| Insert Paragraph Below | none | Insert a blank paragraph below the current line or selected line range and place the caret in the inserted paragraph. |

`Code Block`, `Table`, and `Horizontal Rule` should be duplicated into Paragraph for this first version because they are block-structure operations in the screenshot and already have stable behavior. Keep the existing Insert menu entries during this pass so users do not lose the current path.

`Insert Paragraph Above` and `Insert Paragraph Below` are line-based operations in the first version, not Markdown AST block detection. For a multi-line selection, "above" means before the first selected line and "below" means after the last selected line.

## Deferred Items

| Screenshot item | Phase | Reason |
| --- | --- | --- |
| Raise Heading Level | Phase 2 | Needs heading-level detection and multi-line selection behavior. |
| Lower Heading Level | Phase 2 | Same as raise heading level; must handle H1/H6 boundaries. |
| Task List | Phase 2 | Needs marker rules for `[ ]` insertion and selected-line handling. |
| Task Status submenu | Phase 2 | Needs current task-state detection and toggle/checked/unchecked semantics. |
| List Indent submenu | Phase 2 | Needs nested-list indentation and ordered-list renumbering rules. |
| Alert / Warning Box submenu | Phase 3 | Requires an agreed admonition syntax and renderer support. |
| Equation Block | Phase 3 | Requires math syntax and rendering support. |
| Link Reference | Phase 2 | Needs reference-label generation and duplicate handling. |
| Footnote | Phase 2 | Needs parser/rendering support confirmation and unique footnote IDs. |
| Table of Contents | Phase 2 | Needs heading anchor generation and update behavior. |
| YAML Front Matter | Phase 2 | Simple insertion, but belongs to document metadata rather than paragraph editing. |

## Architecture

### MainWindow.xaml

`MainWindow.xaml` remains the owner of the top menu layout. The Paragraph popup should be expanded in place and continue using existing menu styles.

Responsibilities:

- Render the first-version Paragraph menu groups.
- Show shortcut labels in `Tag`.
- Keep deferred items out of the UI.
- Route simple app-level UI actions such as table insertion to existing handlers.
- Duplicate Code Block, Table, and Horizontal Rule into Paragraph without removing their existing Insert menu entries.

`MainWindow.xaml` should not contain editing algorithms.

### MainWindow.EditorUi.cs

`MainWindow.EditorUi.cs` should remain a thin bridge between menu clicks and editor operations.

Allowed responsibilities:

- Close the active popup where needed.
- Call a focused editor method or paragraph operation helper.
- Open `TableInsertDialog` for table insertion.

Avoid adding line parsing, selection range calculation, list indentation, heading-level math, or multi-line edit algorithms here.

### MarkdownEditor

`MarkdownEditor` remains the reusable text editing surface.

First-version responsibilities:

- Expose existing simple operations to the sample app.
- Apply text operations inside one undo unit when multi-step text edits are needed.
- Keep `Markdown` synchronized with `TextBox.Text` after programmatic edits.
- Preserve caret and selection behavior after text edits.

The existing `ToggleLinePrefix` can cover some first-version items, but it currently has a broad regex and only handles a narrow set of prefixes. If first-version operations need selected-line behavior or Paragraph cleanup, use a dedicated helper instead of expanding ad hoc logic inside `MainWindow`.

### Paragraph Text Operations Helper

Add a focused, stateless helper when implementation begins, for example:

`src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs`

The helper should be pure and testable:

- Input: source text, selection start, selection length, requested block operation.
- Output: `TextEditOperation`.
- No WPF controls.
- No clipboard.
- No localization.
- No sample app references.

Candidate operations:

- `SetHeadingLevel(level)`
- `ClearBlockPrefix()`
- `ToggleBlockquote()`
- `ToggleOrderedList()`
- `ToggleBulletList()`
- `InsertParagraphAbove()`
- `InsertParagraphBelow()`
- `InsertHorizontalRule()`

`MarkdownEditor` or `MainWindow.EditorUi.cs` can then apply the returned `TextEditOperation` using the existing single-undo-unit pattern from editor commands.

## Data Flow

Simple menu command flow:

```text
Top Paragraph menu item
  -> MainWindow.EditorUi thin handler
  -> MarkdownEditor operation or MarkdownParagraphOperations
  -> TextEditOperation
  -> MarkdownEditor applies text edit
  -> Markdown property updates
  -> preview debounce and dirty-state handling
```

Table command flow:

```text
Top Paragraph menu Table
  -> MainWindow.EditorUi opens TableInsertDialog
  -> MarkdownInsertService.GenerateTable(rows, columns)
  -> Editor.InsertText(markdownTable)
```

This mirrors the existing table behavior and avoids inventing a new table workflow.

## Localization

Add or reuse localization keys in:

- `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`

Existing keys already cover:

- Heading 1
- Heading 2
- Heading 3
- Blockquote
- Ordered List
- Bullet List
- Code Block
- Table
- Horizontal Rule

Add these first-version keys:

| Key | English | Chinese |
| --- | --- | --- |
| `MainWindow.Heading4` | Heading 4 | 四级标题 |
| `MainWindow.Heading5` | Heading 5 | 五级标题 |
| `MainWindow.Heading6` | Heading 6 | 六级标题 |
| `MainWindow.ParagraphStyle` | Paragraph | 段落 |
| `MainWindow.InsertParagraphAbove` | Insert Paragraph Above | 在上方插入段落 |
| `MainWindow.InsertParagraphBelow` | Insert Paragraph Below | 在下方插入段落 |

## Testing Strategy

### Pure Operation Tests

Add tests for `MarkdownParagraphOperations`:

- `SetHeadingLevel_CurrentLine_AddsRequestedHeadingMarker`
- `SetHeadingLevel_ExistingHeading_ReplacesHeadingMarker`
- `SetHeadingLevel_SelectedLines_UpdatesEachSelectedLine`
- `ClearBlockPrefix_HeadingLine_RemovesHeadingMarker`
- `ClearBlockPrefix_BlockquoteLine_RemovesQuoteMarker`
- `ClearBlockPrefix_ListLine_RemovesListMarker`
- `ToggleBlockquote_CurrentLine_TogglesPrefix`
- `ToggleBulletList_CurrentLine_TogglesPrefix`
- `ToggleOrderedList_CurrentLine_TogglesPrefix`
- `InsertParagraphAbove_CurrentLine_InsertsBlankParagraphBeforeLine`
- `InsertParagraphBelow_CurrentLine_InsertsBlankParagraphAfterLine`
- `InsertParagraphAbove_SelectedLines_InsertsBeforeFirstSelectedLine`
- `InsertParagraphBelow_SelectedLines_InsertsAfterLastSelectedLine`
- `InsertHorizontalRule_BetweenText_AddsBlankLineBoundaries`

### WPF Integration Tests

Add focused WPF tests only for editor application behavior:

- `ParagraphMenu_Heading4_UpdatesMarkdown`
- `ParagraphMenu_Paragraph_ClearsHeadingMarker`
- `ParagraphMenu_HorizontalRule_InsertsBlockWithBoundaries`
- `ParagraphMenu_Table_InvokesExistingTableInsertionPath`

### Localization Tests

Add or update tests that verify English and Chinese keys exist for new menu labels.

### Menu Shape Tests

Add a sample app menu test for first-version menu shape:

- H1-H6 present.
- Paragraph present.
- Insert Paragraph Above/Below present.
- First-version block items present.
- Deferred screenshot items absent.

## Acceptance Criteria

- Paragraph menu exposes only approved first-version items.
- H1-H6, Paragraph, quote, ordered list, bullet list, code block, table, horizontal rule, and insert paragraph above/below are available.
- Existing Insert menu entries for code block, table, and horizontal rule remain available.
- Deferred screenshot items are not shown in the first-version UI.
- `MainWindow.EditorUi.cs` remains a thin bridge and does not gain text parsing algorithms.
- New text algorithms live in a focused WPF-layer helper with pure operation tests.
- Text edits preserve one undo unit where operations rewrite multiple characters or lines.
- `Markdown` stays synchronized with the editor text.
- English and Chinese localization keys are complete.
- Relevant pure operation tests and WPF integration tests pass.
