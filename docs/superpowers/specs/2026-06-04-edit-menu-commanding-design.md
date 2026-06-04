# Edit Menu Commanding Design

## Summary

Expand the sample application's top Edit menu using WPF's built-in commanding model instead of introducing a separate edit-menu ViewModel or Sample-layer command service. The Edit menu is an editor command source: built-in commands route to the editor `TextBox`, custom editor commands route to `MarkdownEditor`, and only app-level actions remain in `MainWindow`.

This differs from the File menu architecture on purpose. File menu commands are window/application operations that coordinate dialogs, filesystem services, current document state, and sidebar state. Edit menu commands are mostly text-editor operations, so their natural command target is the editor control.

## Official Architecture Basis

Microsoft's WPF commanding guidance defines four roles: command, command source, command target, and command binding. Menu items, buttons, and key gestures act as command sources; controls such as `TextBox` act as command targets; command bindings connect custom commands to execution logic.

Relevant official references:

- WPF Commanding Overview: `https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview`
- WPF Data Binding Overview: `https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/`
- .NET Dependency Injection: `https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview`

The design follows that guidance:

- Use `ApplicationCommands` for built-in text actions.
- Add custom routed editor commands only for behavior WPF does not already provide.
- Keep services for IO, conversion, and app state where DI is useful.
- Do not force editor selection, caret, or undo-stack state into a ViewModel.

## Current State

The sample app already has these foundations:

- `MainWindow.xaml` owns the top menu UI and currently contains a small Edit menu with undo, redo, and find.
- `MainWindow.EditorUi.cs` routes formatting, paragraph, insert, undo, redo, find, search panel, sidebar, and outline behavior.
- `MarkdownEditor` exposes the source `TextBox` through `TextBox` and already registers command bindings for `ApplicationCommands.Undo`, `Redo`, `Cut`, `Copy`, `Paste`, and `SelectAll`.
- `MarkdownEditor` already has custom paste handling for clipboard images, copied image files, and text.
- The editor's context menu already exposes localized undo, redo, cut, copy, paste, and select all.
- `MarkdownInsertService` and `MarkdownSearchService` provide small, testable helpers for table generation and search.
- File menu work uses a different pattern: `MainWindow` coordinates app-level commands, while `FileOperationService`, `RecentFilesService`, `WorkspaceViewModel`, and `RecentFilesMenuViewModel` handle non-UI state or IO.

## Goals

- Expand the top Edit menu to cover the approved screenshot-aligned command set.
- Route editor operations through WPF commanding rather than `MainWindow` event handlers where possible.
- Keep `MainWindow` free of text-editing algorithms.
- Reuse existing `MarkdownEditor` behavior for paste image and standard text commands.
- Add only focused, editor-owned custom commands for first-version custom behavior.
- Keep app-level commands, such as find and future replace UI, in the sample application.
- Keep localization synchronized across `LocalizationStrings.cs` and XAML resource dictionaries.
- Avoid new dependencies.

## Non-goals

- Do not create `EditMenuView`, `EditMenuViewModel`, or a Sample-layer command catalog for the first version. The Edit menu state is driven by WPF command routing and each command target's `CanExecute`, so an extra ViewModel would duplicate command state.
- Do not move file-menu architecture to the Edit menu. The command target is different.
- Do not add a full rich clipboard conversion pipeline in the first version.
- Do not implement spell-check workflows, smart punctuation, emoji pickers, or math rendering in the first version.
- Do not introduce multi-document or tab-aware editing.
- Do not place text algorithms in `MainWindow.xaml.cs`.

## Approved Menu Scope

The screenshot's Edit menu is divided into first-version commands, second-phase commands, and deferred research.

### First Version

| Menu item | Shortcut | Behavior |
| --- | --- | --- |
| Undo | `Ctrl+Z` | Route to `ApplicationCommands.Undo` on the editor text box. |
| Redo | `Ctrl+Y` | Route to `ApplicationCommands.Redo` on the editor text box. |
| Cut | `Ctrl+X` | Route to `ApplicationCommands.Cut`. |
| Copy | `Ctrl+C` | Route to `ApplicationCommands.Copy`. |
| Paste Image | none | Route to an editor command that only handles clipboard images or copied image files and inserts Markdown image syntax. Disable when no image source is available. |
| Paste | `Ctrl+V` | Route to `ApplicationCommands.Paste`, preserving existing image-first paste behavior. |
| Copy as Plain Text | none | Copy selected source text as plain text. Disable when there is no selection. |
| Paste as Plain Text | `Ctrl+Shift+V` | Insert clipboard plain text and ignore non-text formats. |
| Select All | `Ctrl+A` | Route to `ApplicationCommands.SelectAll`. |
| Move Line Up | `Alt+Up` | Move current line or selected line block up as one undoable text edit. |
| Move Line Down | `Alt+Down` | Move current line or selected line block down as one undoable text edit. |
| Delete | none | Delete selected text, or delete the current line when there is no selection. |
| Find | `Ctrl+F` | Keep routing to the sample app search panel. |
| Insert Hard Line Break | none | Insert Markdown hard line break text at the caret. |

### Second Phase

| Menu item | Reason for deferral |
| --- | --- |
| Copy as Markdown | In the source editor this is mostly equivalent to copy. Add only when its product meaning differs, such as copying normalized Markdown from preview or from selected blocks. |
| Copy as HTML Code | Existing `HtmlExportService` is document-level. Selection-to-HTML needs a reusable conversion path and clear selection semantics. |
| Copy Content and Simplify Format | Needs explicit normalization rules before implementation. |
| Copy Image | The screenshot label is "拷贝图片", but the source editor has no selectable image object. Add only when preview-image selection or rendered-image copy is designed. |
| Delete Range | Needs a range model, such as current paragraph, current list, or current heading section. |
| Math Tools | Template insertion is simple, but useful math tooling depends on parser/rendering support and command semantics. |
| Whitespace and Line Break Cleanup | Batch document changes need explicit rules and a single undo unit. |
| Replace | Requires expanding the search overlay with replace-current, replace-all, and result-state behavior. |

### Deferred Research

| Menu item | Reason for deferral |
| --- | --- |
| Smart Punctuation | Must avoid modifying code blocks, inline code, URLs, and existing Markdown punctuation. Needs language-aware rules. |
| Spell Check | WPF `SpellCheck` can be enabled, but a menu-grade workflow needs language, dictionary, and Markdown exclusion rules. |
| Emoji and Symbols | Windows already provides `Win+.`. A built-in picker would be a separate UI feature. |
| Rich clipboard Markdown/HTML conversion | Requires a conversion pipeline, not a menu-only change. |
| Math rendering | Belongs with parser/renderer roadmap work, not the Edit menu first version. |

## Architecture

### MainWindow.xaml

`MainWindow.xaml` remains the owner of the top menu layout, consistent with the File menu implementation. The Edit menu should be expanded in place rather than extracted into a separate control.

Responsibilities:

- Render the top Edit menu groups and separators.
- Use the existing `MenuItemStyle` and shortcut text conventions.
- Bind built-in commands directly to WPF commands.
- Bind custom editor commands to `MarkdownEditorCommands`.
- Route app-level menu items, such as Find, to existing sample app handlers.

The XAML should prefer command binding over `Click` handlers for editor-level commands. `Click` handlers remain acceptable for app-level actions that open sample UI, such as Find or future Replace.

The top menu must set `CommandTarget` explicitly. WPF's default command target follows keyboard focus, and opening a menu or popup can move focus away from the editor. Relying on implicit focus can make `CanExecute` report false even when the editor has a valid selection or caret position.

Recommended XAML shape for built-in commands:

```xml
<Button Command="ApplicationCommands.Copy"
        CommandTarget="{Binding ElementName=Editor, Path=TextBox}"
        Content="{DynamicResource Loc.Editor.Copy}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+C" />
```

Recommended XAML shape for custom editor commands:

```xml
<Button Command="ctrl:MarkdownEditorCommands.MoveLineUp"
        CommandTarget="{Binding ElementName=Editor}"
        Content="{DynamicResource Loc.MainWindow.MoveLineUp}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Alt+Up" />
```

If `ElementName` binding cannot resolve from inside a `Popup`, keep the same target rule but set the target through a small view-level helper when the menu opens. Do not fall back to implicit focus routing.

### MarkdownEditor

`MarkdownEditor` is the target for reusable editor commands.

Responsibilities:

- Continue exposing the internal `TextBox` through `TextBox`.
- Keep existing standard command bindings.
- Add command bindings for custom editor commands:
  - `PasteImage`
  - `CopyPlainText`
  - `PastePlainText`
  - `MoveLineUp`
  - `MoveLineDown`
  - `DeleteSelectionOrCurrentLine`
  - `InsertHardLineBreak`
- Implement `CanExecute` using current selection, caret, text, and clipboard state.
- Keep text mutations in a single undo unit when a command performs multi-step edits.
- Keep command behavior independent of the sample app.

Undo grouping requirement:

- Wrap multi-step text mutations in `EditorTextBox.BeginChange()` / `EditorTextBox.EndChange()`.
- Prefer applying a computed operation with one affected-range replacement instead of assigning `TextBox.Text` multiple times.
- Restore caret and selection after the replacement inside the same change group.
- Do not build commands from several smaller `TextBox` mutations that would produce multiple undo steps.

### MarkdownEditorCommands

Add a command container under the WPF control layer, for example:

`src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditorCommands.cs`

Responsibilities:

- Define reusable `RoutedUICommand` instances for editor-specific commands.
- Store default input gestures where appropriate.
- Keep command names stable so top menu, context menu, and future toolbar entries can reuse them.

These commands are editor-level behavior, not sample app behavior. They belong beside `MarkdownEditor`, not in `MainWindow`.

### Editor Text Operations

The custom command handlers may call private helper methods in `MarkdownEditor`, or a focused internal helper such as:

`src/WpfMarkdownEditor.Wpf/Controls/EditorTextOperations.cs`

`EditorTextOperations` should be a stateless helper. Prefer static pure functions that receive text and selection inputs, then return an operation description. The helper should not hold a `TextBox`, `MarkdownEditor`, dispatcher, clipboard, or mutable editor state.

Recommended shape:

```csharp
internal readonly record struct TextEditOperation(
    string Text,
    int SelectionStart,
    int SelectionLength);
```

Responsibilities:

- Compute current-line and selected-line ranges.
- Move selected line blocks up or down.
- Delete current line when no selection exists.
- Insert hard line breaks.
- Compute plain-text insertion output from supplied clipboard text.

`MarkdownEditor` command bindings are responsible for reading UI state, reading clipboard state, checking `CanExecute`, applying the returned `TextEditOperation`, and grouping undo changes. This keeps algorithm tests free of STA requirements when they do not instantiate WPF controls.

The helper should not know about `MainWindow`, dialogs, recent files, status text, localization, or WPF controls.

### Sample App Commands

Commands that are not pure editor behavior remain in the sample app.

Examples:

- `Find`: opens the existing search panel.
- Future `Replace`: expands the search panel or opens a sample app overlay.
- Future `Copy as HTML Code`: may use `HtmlExportService` and sample-level selection rules.

This keeps reusable editor behavior in `WpfMarkdownEditor.Wpf` and application workflows in `WpfMarkdownEditor.Sample`.

## Data Flow

Built-in command flow:

```text
Top Edit menu button
  -> ApplicationCommands.Copy/Cut/Paste/etc.
  -> explicit CommandTarget = editor TextBox
  -> TextBox/MarkdownEditor command binding
  -> Text changes
  -> MarkdownEditor.MarkdownChanged
  -> existing dirty state, preview debounce, and outline update
```

Custom editor command flow:

```text
Top Edit menu button
  -> MarkdownEditorCommands.MoveLineUp
  -> explicit CommandTarget = MarkdownEditor
  -> MarkdownEditor CommandBinding CanExecute/Executed
  -> EditorTextOperations computes TextEditOperation
  -> MarkdownEditor applies TextEditOperation inside BeginChange/EndChange
  -> MarkdownEditor.MarkdownChanged
```

Paste command relationship:

- `ApplicationCommands.Paste` remains the normal paste path and preserves the existing image-first behavior: image clipboard, copied image file, then text.
- `MarkdownEditorCommands.PasteImage` is an explicit image-only action. It is enabled only when the clipboard contains an image or a file-drop list with a supported image file.
- `PasteImage` must ignore text-only clipboard content. If the clipboard has text but no image source, `CanExecute` is false and the menu item is disabled.
- `PastePlainText` is text-only. It is enabled only when the clipboard contains text.

Sample app command flow:

```text
Top Edit menu Find
  -> MainWindow.OnFind
  -> ShowSearchPanel()
  -> MarkdownSearchService.FindMatches(...)
```

## Localization

Reuse `Editor.*` keys for labels that are identical between the top Edit menu and the `MarkdownEditor` context menu, such as Undo, Redo, Cut, Copy, Paste, and Select All. Add `MainWindow.*` keys only for top-menu-only entries or labels that intentionally differ from the context menu.

This avoids maintaining duplicate translations for the same user-visible command. If a future product decision requires different wording in the top menu and context menu, document that difference beside the key addition.

All additions must be synchronized in:

- `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`

Use screenshot-aligned Chinese labels for the top menu where possible:

- 撤销
- 重做
- 剪切
- 复制
- 粘贴图片
- 粘贴
- 复制为纯文本
- 粘贴为纯文本
- 选择
- 上移该行
- 下移该行
- 删除
- 查找和替换

The screenshot's "拷贝图片" label is deferred as Copy Image until rendered image selection/copy behavior exists.

## Testing Strategy

### WPF Command Tests

Add focused WPF tests for `MarkdownEditor` custom commands:

- `PastePlainText_ClipboardHasText_InsertsTextOnly`
- `CopyPlainText_SelectionCopiesPlainText`
- `MoveLineUp_CurrentLineSwapsWithPreviousLine`
- `MoveLineDown_SelectedLinesMoveAsBlock`
- `DeleteSelectionOrCurrentLine_NoSelectionDeletesCurrentLine`
- `InsertHardLineBreak_InsertsMarkdownHardBreak`
- `CopyPlainTextCanExecute_NoSelection_Disabled`
- `PastePlainTextCanExecute_ClipboardHasNoText_Disabled`
- `PasteImageCanExecute_ClipboardHasImage_Enabled`
- `PasteImageCanExecute_ClipboardHasOnlyText_Disabled`
- `MoveLineUpCanExecute_CaretOnFirstLine_Disabled`
- `MoveLineDownCanExecute_CaretOnLastLine_Disabled`
- `DeleteSelectionOrCurrentLineCanExecute_EmptyDocument_Disabled`

Tests that instantiate WPF controls must use the existing STA test host pattern.

### Menu Routing Tests

Add or update sample app routing tests similar to the File menu tests:

- Edit menu contains the approved first-version items.
- Edit menu omits or marks deferred screenshot items.
- Built-in items use commands instead of `Click` handlers where appropriate.
- Editor-level menu items set explicit `CommandTarget` bindings.
- Find remains routed to sample app search UI.
- Shortcut labels match the approved menu scope.

### Localization Tests

Add localization coverage for new keys in English and Chinese.

### Service Tests

No Sample-layer `EditorEditService` is planned for the first version. If selection-to-HTML or other sample-level conversion is added later, test that service separately.

## Acceptance Criteria

- The top Edit menu matches the first-version approved scope.
- Built-in text commands route through WPF commanding.
- Custom editor operations are command bindings on `MarkdownEditor`.
- `MainWindow` does not contain text-editing algorithms.
- Existing context menu behavior continues to work.
- Existing paste image behavior is preserved.
- New localization keys exist in English and Chinese.
- Focused WPF command tests and menu routing tests pass.
- No new dependencies are added.

## Implementation Notes

- Prefer command sources over `Click` handlers for editor-level commands.
- If a custom command needs explicit command target binding from `MainWindow.xaml`, target `Editor` for `MarkdownEditorCommands` and target `Editor.TextBox` for `ApplicationCommands` when feasible.
- If Popup focus breaks command target resolution, set the command target explicitly rather than relying on keyboard focus.
- Keep keyboard shortcut routing aligned with command gestures. Avoid duplicating command logic in `OnPreviewKeyDown`.
- Keep deferred menu items out of the first-version UI unless the product decision is to show them disabled.
