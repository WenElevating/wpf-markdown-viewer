# Table Insert Dialog Design

## Summary

Replace the hardcoded table insertion with a dialog that lets users specify row and column counts before generating a markdown table.

## Current Behavior

`MainWindow.xaml.cs:384` — clicking Insert > Table directly inserts a fixed 3x3 table via `Editor.InsertText(...)`.

## New Behavior

1. Click Insert > Table
2. Dialog opens with two number spinners (Rows, Columns)
3. User adjusts values and clicks Insert (or Cancel)
4. Markdown table is generated and inserted at cursor

## Dialog: TableInsertDialog

**Location:** `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml` (alongside SaveConfirmationDialog)

**Style:** Same borderless pattern -- `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, rounded corners, drop shadow.

**Layout:**
- Width: ~320px, `SizeToContent="Height"`
- Header: table icon (Segoe MDL2 Assets) + "Insert Table" title
- Input section: two labeled rows
  - "Rows" -- Number spinner, min 1, max 20, default 2
  - "Columns" -- Number spinner, min 1, max 10, default 3
- Buttons: Cancel (secondary) / Insert (accent), right-aligned

**Number Spinner:** Two RepeatButton arrows flanking a read-only TextBox showing the current value. All within a bordered container matching the project's input style.

## Generation Logic

- The "Rows" value is the number of data rows (excluding header). So Rows=2 produces header + 1 data row.
- Header cells: "Column 1", "Column 2", ...
- Data cells: "Cell 1", "Cell 2", ... (sequential numbering)
- Separator row uses `| --- |` alignment.
- Output wrapped with leading/trailing newlines.

**Example (Rows=2, Columns=3):**
```markdown

| Column 1 | Column 2 | Column 3 |
| -------- | -------- | -------- |
| Cell 1   | Cell 2   | Cell 3   |

```

## Files Changed

| File | Change |
|------|--------|
| `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml` | New -- dialog XAML |
| `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml.cs` | New -- dialog code-behind (row/col spinners, Insert/Cancel, Result property) |
| `samples/WpfMarkdownEditor.Sample/MainWindow.xaml.cs` | Modify `OnTable` to show dialog and generate table |

No changes to Core or Wpf library projects. No new dependencies.

## Dialog Result Pattern

Follow `SaveConfirmationDialog` pattern:
- `public (int Rows, int Columns)? Result` property
- `DialogResult = true` on Insert, `DialogResult = false` on Cancel
- `Owner = mainWindow` set by caller

## Caller Code (MainWindow.xaml.cs)

```csharp
private void OnTable(object sender, RoutedEventArgs e)
{
    InsertPopup.IsOpen = false;
    var dialog = new TableInsertDialog { Owner = this };
    if (dialog.ShowDialog() == true && dialog.Result is (int rows, int cols))
    {
        var table = GenerateTable(rows, cols);
        Editor.InsertText(table);
    }
}

private static string GenerateTable(int dataRows, int columns)
{
    var sb = new StringBuilder();
    sb.Append('\n');

    // Header
    sb.Append("| ");
    sb.Append(string.Join(" | ", Enumerable.Range(1, columns).Select(i => $"Column {i}")));
    sb.Append(" |\n");

    // Separator
    sb.Append("| ");
    sb.Append(string.Join(" | ", Enumerable.Repeat("--------", columns)));
    sb.Append(" |\n");

    // Data rows
    var cellIndex = 1;
    for (var r = 0; r < dataRows; r++)
    {
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Range(0, columns).Select(_ => $"Cell {cellIndex++}")));
        sb.Append(" |\n");
    }

    return sb.ToString();
}
```
