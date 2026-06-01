# File Menu and Workspace Design

## Summary

Expand the sample application's File menu to match the reference menu shape while staying aligned with the current single-document-per-window editor model. The first implementation adds independent new windows, folder browsing in the sidebar, global persistent recent files, quick open, save-as, file operations, import, HTML export, print, preferences, and close.

The first version explicitly excludes "Save All Open Files" because the application does not yet have a multi-document session or tab model. Hiding the item is clearer than showing a disabled command that users cannot act on.

## Current State

The sample app already has these foundations:

- `MainWindow` owns one editable document through `_currentFilePath`, `_isDirty`, and `_loadingFile`.
- Existing file operations include new file, open file, save, dirty-file confirmation, and title/status updates.
- `MainWindow` can be constructed with an optional file path.
- The sidebar has `History` and `Outline` tabs.
- A window-local `_fileHistory` powers the sidebar history list, but it is not persisted and is not shared across windows.
- UI localization already supports English and Chinese resources.

The implementation should preserve this model rather than introduce tabs or a global document graph.

## Goals

- Rework the File menu to cover the approved screenshot-aligned command set.
- Add independent blank windows through `Ctrl+Shift+N`.
- Add folder selection and a Markdown file tree in the sidebar.
- Add global persistent recent files shared by all windows and preserved across restarts.
- Add quick open over recent files and the current folder tree.
- Add save-as, move, delete, file properties, reveal in Explorer, reveal in sidebar, import, HTML export, print, preferences, and close.
- Keep shortcuts as routing only. `OnPreviewKeyDown` must detect key combinations and dispatch to command methods; it must not contain file operation logic.
- Keep new services in the sample app unless a capability is clearly reusable library behavior.
- Avoid new dependencies.

## Non-goals

- Do not implement "Save All Open Files" in the first version.
- Do not add multi-tab editing.
- Do not add PDF export.
- Do not add session restore.
- Do not persist the opened folder workspace across app restart.
- Do not add real-time recent-file push updates across open windows. Menus reload persisted recent files when opened.
- Do not move application-specific file history or workspace persistence into `WpfMarkdownEditor.Wpf`.

## Approved Menu Scope

The File menu should be grouped like the reference menu:

| Menu item | Shortcut | First-version behavior |
| --- | --- | --- |
| New | `Ctrl+N` | Clear the current window after dirty confirmation. |
| New Window | `Ctrl+Shift+N` | Create an independent blank `MainWindow`. |
| Open... | `Ctrl+O` | Open a Markdown file in the current window after dirty confirmation. |
| Open Folder... | none | Select a folder and show its Markdown file tree in the sidebar. |
| Quick Open... | `Ctrl+P` | Search recent files and current folder-tree files. |
| Open Recent File | submenu | Show global persistent recent files. |
| Save | `Ctrl+S` | Save current file, or prompt with Save As for untitled content. |
| Save As... | `Ctrl+Shift+S` | Save to a selected path and make that path the current file. |
| Move To... | none | Move the current file to a selected path and update current state. |
| Properties... | none | Show read-only file metadata for the current file. |
| Open File Location... | none | Open Explorer with the current file selected. |
| Show in Sidebar | none | Open the sidebar, switch to Files, and select the current file when possible. |
| Delete... | none | Confirm, delete the current file, then switch to an untitled empty document. |
| Import... | none | Append Markdown/text file contents to the current document. |
| Export | submenu | First version supports HTML export only. |
| Print... | `Alt+Shift+P` | Print the rendered preview only when it meets the printable conditions below, otherwise print Markdown text. |
| Preferences... | `Ctrl+,` | Open `PreferencesDialog` and apply changes through existing language, theme, and translation settings paths. |
| Close | `Ctrl+W` | Close the current window with dirty confirmation. |

## Architecture

### MainWindow

`MainWindow` remains the owner of one current document. Existing fields remain the source of truth for the current file path and dirty state.

New command methods should be small, named by user intent, and call services or existing helpers:

- `NewFile()`
- `NewWindow()`
- `OpenFile()`
- `OpenFolder()`
- `QuickOpen()`
- `OpenRecentFile(string path)`
- `SaveCurrentFileAsync()`
- `SaveCurrentFileAsAsync()`
- `MoveCurrentFile()`
- `ShowCurrentFileProperties()`
- `OpenCurrentFileLocation()`
- `ShowCurrentFileInSidebar()`
- `DeleteCurrentFile()`
- `ImportFileIntoDocument()`
- `ExportCurrentDocumentAsHtml()`
- `PrintCurrentDocument()`
- `OpenPreferences()`
- `CloseCurrentWindow()`

`OnPreviewKeyDown` should do only this:

1. Match shortcut combinations.
2. Set `e.Handled = true`.
3. Dispatch to the relevant command method.

No file dialogs, disk IO, deletion, moving, scanning, printing, or export logic belongs in `OnPreviewKeyDown`.

### RecentFilesService

Location: `samples/WpfMarkdownEditor.Sample/RecentFilesService.cs`

Responsibilities:

- Store recent files at `%LOCALAPPDATA%\WpfMarkdownEditor.Sample\recent-files.json`.
- Return recent file entries ordered most-recent first.
- Add or refresh an entry when a file is opened, saved, saved as, or moved.
- Remove entries for missing files when detected by menu open or command execution.
- Keep at most 20 entries.
- Treat malformed JSON as an empty list.
- Serialize cross-window and cross-process writes so simultaneous windows do not corrupt the JSON file.

Data shape:

```json
{
  "files": [
    {
      "path": "D:\\Docs\\note.md",
      "openedAt": "2026-06-01T10:30:00Z"
    }
  ]
}
```

The current window-local `_fileHistory` may remain for the sidebar `History` tab. It should not be reused for the global recent-file menu.

Concurrency strategy:

- Use a named per-user mutex, for example `Local\WpfMarkdownEditor.Sample.RecentFiles`, around every read-modify-write operation.
- Inside the mutex, read the current persisted list again, merge the requested change, cap to 20 entries, then write a complete replacement.
- Write to a temporary file in the same directory, flush it, then replace or move it over `recent-files.json` so readers never see a partial JSON document.
- Retry transient `IOException` / `UnauthorizedAccessException` failures up to three times with a short backoff.
- If the mutex cannot be acquired within a small timeout, skip only the recent-file update and show a non-blocking status message. Opening or saving the actual document must not fail only because recent history could not be updated.
- If two windows add different files at nearly the same time, the later writer wins ordering, but both entries should remain because each writer rereads and merges under the mutex.

### FolderWorkspaceService

Location: `samples/WpfMarkdownEditor.Sample/FolderWorkspaceService.cs`

Responsibilities:

- Scan a chosen folder and produce a tree of Markdown files.
- Include `.md`, `.markdown`, and `.mdown`.
- Skip hidden directories, `.git`, `bin`, and `obj`.
- Sort directories before files, then by display name using ordinal-ignore-case comparison.
- Return a data model that the UI can render without additional disk traversal.

The service should be read-only. It does not persist the selected folder and does not own open-file state.

Performance boundaries:

- Run folder scanning on a background task with a cancellation token owned by the current `MainWindow`.
- Cancel any in-flight scan when the user chooses another folder or closes the window.
- Default limits:
  - Maximum Markdown files returned: 5,000.
  - Maximum filesystem entries inspected: 50,000.
  - Maximum recursion depth below the selected folder: 12.
- Return a `FolderWorkspaceResult` that includes `Root`, `IsTruncated`, `MarkdownFileCount`, and `InspectedEntryCount`.
- If limits are reached, render the partial tree and show a localized status message explaining that results were truncated.
- Skip inaccessible directories and record a skipped count rather than failing the whole scan.
- Keep the previous tree visible until the new scan succeeds; if the scan fails before producing a result, leave the previous tree unchanged.

### FileOperationService

Location: `samples/WpfMarkdownEditor.Sample/FileOperationService.cs`

Responsibilities:

- Move current files.
- Delete current files.
- Read file properties.
- Open file location in Explorer.
- Provide focused, testable wrappers around filesystem operations.

The service should not show UI. It returns results or throws exceptions that `MainWindow` converts into localized messages.

Move overwrite flow:

1. `MainWindow` asks the user for the destination path.
2. `MainWindow` checks whether the destination exists.
3. If it exists, `MainWindow` shows the overwrite confirmation dialog.
4. `MainWindow` calls `FileOperationService.MoveFile(source, destination, overwrite: true | false)`.
5. `FileOperationService` performs the requested operation without UI and throws if the destination exists while `overwrite` is `false`.

### Preferences Dialog

First version should be a compact sample-app dialog. It centralizes existing settings behavior without creating a parallel settings model:

- Language selection.
- Theme selection.
- Translation settings.

Dialog strategy:

- Extract the theme list into a small `ThemeCatalog` or equivalent data source so `MainWindow` and `PreferencesDialog` do not maintain separate theme definitions.
- Pass the current `LocalizationService`, current theme name, and theme catalog into `PreferencesDialog`.
- The dialog exposes selected language/theme values or raises simple events; `MainWindow` remains responsible for applying language through the existing localization path and applying theme through the existing `ApplyTheme` path.
- The translation settings button opens the existing `TranslationConfigDialog` through `MainWindow`, keeping translation settings persistence in the existing service.
- The dialog should not duplicate provider configuration storage, theme persistence, or language persistence logic.

## Sidebar Design

Add a `Files` tab to the existing sidebar beside `History` and `Outline`.

Use a WPF `TreeView` with a `HierarchicalDataTemplate` bound to observable workspace nodes:

```csharp
public sealed class WorkspaceTreeNode
{
    public string Name { get; init; }
    public string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public IReadOnlyList<WorkspaceTreeNode> Children { get; init; }
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
}
```

`IsExpanded` and `IsSelected` are bound from `TreeViewItem` container style setters so command logic can select and expand nodes through the view model. `MainWindow` should also keep a normalized path index for the current tree, mapping full paths to nodes and ancestor chains. This avoids repeatedly traversing the visual tree for commands such as `Show in Sidebar`.

When the user opens a folder:

1. Store the selected folder in the current `MainWindow` instance.
2. Scan the folder through `FolderWorkspaceService`.
3. Render the Markdown file tree under the `Files` tab.
4. Open the sidebar if it is closed.
5. Switch to the `Files` tab.

Clicking a file tree item:

1. Runs dirty-file confirmation on the current document.
2. Loads the selected file into the current window.
3. Updates `_currentFilePath`, `_isDirty`, title, status, global recent files, and window-local history.

`Show in Sidebar`:

- Opens the sidebar and selects the `Files` tab.
- Uses `Path.GetRelativePath` and normalized absolute paths to verify the current file is under the opened folder.
- Looks up the current file in the workspace path index.
- Sets expansion state for each ancestor directory, selects the target node, and uses `Dispatcher` after container generation to call `BringIntoView` on the selected `TreeViewItem`.
- Shows a localized status message if no folder is open or if the file is outside the opened folder.
- Does not scan the disk again; if the file is not present in the existing tree, report that it is not in the current workspace view.

## Quick Open

Quick Open should be a lightweight modal or overlay, not a full settings screen.

Sources:

- Global recent files from `RecentFilesService`.
- Current folder-tree files if a folder is open.

Behavior:

- `Ctrl+P` opens the overlay and focuses the search box.
- Typing filters by file name and path.
- Results show file name and parent folder path.
- Selecting a result opens it in the current window after dirty confirmation.
- Missing recent files are removed from persistence and skipped.
- `Escape` closes the overlay and restores focus to the editor.
- `Enter` opens the selected result.
- Up/down arrows move the selected result.
- `Ctrl+P` while the overlay is already open refocuses and selects the search text.
- Clicking outside the overlay or pressing an explicit close button closes it without changing the current document.
- Closing the overlay must not leave keyboard focus trapped in a hidden control.

Quick Open should share open-file logic with recent-file and tree-file commands.

## Import, Export, and Print

### Import

`Import...` selects `.md`, `.markdown`, or `.txt`. The selected file's content is appended to the end of the current document. If the current document is non-empty and does not end with a newline, insert a blank line before the imported content.

Import marks the current document dirty and updates title/status. It does not change `_currentFilePath`.

### Export HTML

`Export > HTML` saves an HTML representation of the current Markdown document to a user-selected path.

Use one concrete export path:

- Add a sample-app `HtmlExportService` that parses the current Markdown with `WpfMarkdownEditor.Core.Parsing.MarkdownParser`.
- Render semantic HTML directly from the parsed block/inline AST.
- Escape text and attribute values with framework HTML encoding APIs.
- Support the parser's existing block and inline types: headings, paragraphs, blockquotes, ordered/unordered lists, fenced code blocks, tables, thematic breaks, links, images, emphasis, strong emphasis, strikethrough, inline code, text, and line breaks.
- Emit a complete UTF-8 HTML document with a small embedded stylesheet.
- Do not convert `FlowDocument` to HTML.
- Do not wrap raw Markdown text as preformatted HTML except as an explicit error fallback that should not be used in the normal path.
- Do not add a third-party Markdown or HTML dependency.

### Print

`Print...` uses `PrintDialog`.

Preferred source order:

1. Rendered preview document when printable.
2. Plain Markdown text as a fallback.

The preview is printable only when all of these are true:

- The editor's preview pane is enabled through its existing preview visibility state.
- `PreviewViewer.Document` is non-null.
- The document contains at least one block.
- The document is not the internal preview-error document.

If any condition fails, print a plain-text `FlowDocument` built from the current Markdown source. A collapsed or disabled preview pane is not considered printable even if an old document object still exists.

Printing errors should be shown as localized messages without changing document state.

## Error Handling

All user-facing failures should use localized messages and leave document state unchanged unless the operation already succeeded.

Required cases:

- Open/save/move/delete/import/export/print failures show a specific error message.
- Missing recent file is removed from recent files and reported.
- Folder scan failure leaves the previous file tree intact.
- Move target exists prompts before overwrite in `MainWindow`; the service receives the resolved overwrite decision.
- Delete current file requires explicit confirmation.
- Show in Sidebar reports when no folder is open or current file is outside the opened folder.
- Commands that require a current file, such as properties, move, reveal, and delete, should be disabled or show a clear status message when the document is untitled.

## Localization

Add localization keys for every new menu item, dialog caption, confirmation, status, and error message in:

- `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`
- `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`

Menu labels should be localizable. File paths, file names, theme names, provider names, and user content remain user data and should not be translated.

## Testing

Add focused xUnit coverage:

- `RecentFilesServiceTests`
  - Adds entries.
  - Moves existing entries to the top.
  - Caps the list at 20.
  - Removes missing files.
  - Handles malformed JSON as an empty list.
  - Serializes concurrent writes and preserves both entries when two callers update different paths.
  - Leaves document operations nonfatal when recent history cannot be updated.
- `FolderWorkspaceServiceTests`
  - Includes Markdown extensions.
  - Excludes non-Markdown files.
  - Skips `.git`, `bin`, `obj`, and hidden directories.
  - Produces stable sorted output.
  - Enforces file, entry, and depth limits.
  - Returns a truncated result instead of blocking or failing on oversized trees.
  - Skips inaccessible directories without failing the whole scan.
- `FileOperationServiceTests`
  - Moves files.
  - Handles overwrite decisions.
  - Deletes files.
  - Reads properties.
  - Reports missing file errors.
- `MainWindow` or command-routing tests
  - `Ctrl+Shift+N` routes to new-window behavior.
  - `Ctrl+Shift+S` routes to save-as behavior.
  - `Ctrl+W` routes to close-window behavior.
  - `OnPreviewKeyDown` stays a router and does not absorb business logic.
  - Quick Open closes on `Escape` and restores focus.
  - Import appends content instead of replacing the document.
  - Show in Sidebar selects an indexed workspace node without rescanning.
- `HtmlExportServiceTests`
  - Exports representative Markdown blocks and inlines as semantic HTML.
  - HTML-encodes text and attributes.
  - Emits a complete UTF-8 HTML document.
- Localization coverage
  - New keys exist in English and Chinese resource maps.

Verification commands after implementation:

```powershell
dotnet test WpfMarkdownEditor.sln --no-restore
```

Run focused WPF tests if UI behavior is changed in a way that full solution tests do not clearly cover.

## Implementation Boundaries

Keep the first pass reviewable:

- Use existing `MainWindow` patterns for dialogs, status messages, dirty confirmation, and title updates.
- Extract services before adding more large blocks to `MainWindow.xaml.cs`.
- Add no new packages.
- Keep `WpfMarkdownEditor.Wpf` changes limited to reusable localization strings or reusable editor surface methods needed for printing/exporting.
- Prefer hidden command omission over disabled unavailable features for "Save All Open Files".

## Acceptance Criteria

- The File menu exposes the approved command set and omits "Save All Open Files".
- `Ctrl+Shift+N` opens an independent blank window.
- Opening, refreshing, saving, and moving 25 files leaves the recent-file list with the 20 most recent unique existing paths.
- Global recent files are shared across windows, survive application restart, and reload when the recent menu opens.
- Concurrent recent-file updates from two windows do not corrupt `recent-files.json` and preserve both updated paths.
- `Open Folder...` scans off the UI thread, shows a Markdown file tree in the sidebar, and truncates oversized trees at the configured limits with a localized status message.
- `Quick Open...` searches recent files and opened-folder files, opens the selected result with `Enter`, navigates with arrow keys, and closes with `Escape`.
- `Show in Sidebar` expands ancestor directories, selects the current file, and scrolls it into view when it exists in the current workspace tree.
- `Move To...` prompts in `MainWindow` before overwriting an existing target and delegates the resolved operation to `FileOperationService`.
- `Import...` appends imported content to the current document and marks it dirty.
- `Export > HTML` uses the AST-based `HtmlExportService` and does not convert from `FlowDocument` or add dependencies.
- `Print...` uses the preview document only when the preview is printable by the defined conditions; otherwise it prints plain Markdown text.
- `Preferences...` reuses existing language/theme/translation settings paths and does not create duplicate persistence logic.
- `OnPreviewKeyDown` contains shortcut routing only.
- New user-facing text is localized in English and Chinese.
- Focused tests cover services and shortcut routing.
- Full solution tests pass after implementation.
