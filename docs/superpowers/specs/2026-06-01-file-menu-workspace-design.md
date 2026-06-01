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
| Import... | none | Insert Markdown/text file contents into the current document. |
| Export | submenu | First version supports HTML export only. |
| Print... | `Alt+Shift+P` | Print the rendered preview when available, otherwise print Markdown text. |
| Preferences... | `Ctrl+,` | Open a small preferences dialog or hub. |
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

### FolderWorkspaceService

Location: `samples/WpfMarkdownEditor.Sample/FolderWorkspaceService.cs`

Responsibilities:

- Scan a chosen folder and produce a tree of Markdown files.
- Include `.md`, `.markdown`, and `.mdown`.
- Skip hidden directories, `.git`, `bin`, and `obj`.
- Sort directories before files, then by display name using ordinal-ignore-case comparison.
- Return a data model that the UI can render without additional disk traversal.

The service should be read-only. It does not persist the selected folder and does not own open-file state.

### FileOperationService

Location: `samples/WpfMarkdownEditor.Sample/FileOperationService.cs`

Responsibilities:

- Move current files.
- Delete current files.
- Read file properties.
- Open file location in Explorer.
- Provide focused, testable wrappers around filesystem operations.

The service should not show UI. It returns results or throws exceptions that `MainWindow` converts into localized messages.

### Preferences Dialog

First version can be a compact sample-app dialog or hub. It should centralize links to existing settings behavior:

- Language selection.
- Theme selection.
- Translation settings.

Language and theme behavior should reuse existing `LocalizationService`, `BuildLanguageList`, `BuildThemeList`, and theme application paths where practical. The dialog should not create a second settings model unless required by the UI.

## Sidebar Design

Add a `Files` tab to the existing sidebar beside `History` and `Outline`.

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
- Expands and highlights the current file if it is under the opened folder.
- Shows a localized status message if no folder is open or if the file is outside the opened folder.

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

Quick Open should share open-file logic with recent-file and tree-file commands.

## Import, Export, and Print

### Import

`Import...` selects `.md`, `.markdown`, or `.txt`. The selected file's content is inserted into the current document at the current caret position. If the editor does not have a useful caret position, append the content to the end.

Import marks the current document dirty and updates title/status. It does not change `_currentFilePath`.

### Export HTML

`Export > HTML` saves an HTML representation of the current Markdown document to a user-selected path.

The implementation should prefer existing parsing/rendering/conversion paths. If the current reusable converter does not expose direct HTML output, the implementation may add a small sample-app export helper that produces a simple HTML document from the rendered flow content or from Markdown text. It must avoid adding a new dependency in this first version.

### Print

`Print...` uses `PrintDialog`.

Preferred source order:

1. Rendered preview document when accessible.
2. Plain Markdown text as a fallback.

Printing errors should be shown as localized messages without changing document state.

## Error Handling

All user-facing failures should use localized messages and leave document state unchanged unless the operation already succeeded.

Required cases:

- Open/save/move/delete/import/export/print failures show a specific error message.
- Missing recent file is removed from recent files and reported.
- Folder scan failure leaves the previous file tree intact.
- Move target exists prompts before overwrite.
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
- `FolderWorkspaceServiceTests`
  - Includes Markdown extensions.
  - Excludes non-Markdown files.
  - Skips `.git`, `bin`, `obj`, and hidden directories.
  - Produces stable sorted output.
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
- `Open Folder...` shows a Markdown file tree in the sidebar.
- Global recent files are persisted, shared across windows, and survive restart.
- `Quick Open...` searches recent files and opened-folder files.
- Save, Save As, Move, Properties, Open File Location, Show in Sidebar, Delete, Import, Export HTML, Print, Preferences, and Close have implemented first-version behavior.
- `OnPreviewKeyDown` contains shortcut routing only.
- New user-facing text is localized in English and Chinese.
- Focused tests cover services and shortcut routing.
- Full solution tests pass after implementation.
