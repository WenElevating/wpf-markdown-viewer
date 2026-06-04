# Paragraph Menu Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the sample app's top Paragraph menu with stable, immediately usable Markdown block operations.

**Architecture:** Keep `MainWindow.xaml` as the menu layout owner and `MainWindow.EditorUi.cs` as a thin click-to-editor bridge. Add pure WPF-layer text operations in `MarkdownParagraphOperations`, expose narrow application methods on `MarkdownEditor`, and reuse existing insert/table handlers without removing current Insert menu paths.

**Tech Stack:** C#/.NET 8 WPF, xUnit, existing `WpfTestHost`, `TextBox.BeginChange()/EndChange()`, existing localization dictionaries and XAML resource dictionaries.

---

## File Structure

- Create `src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs`: pure static helper that computes paragraph/block text edits from text plus selection state. It returns existing `TextEditOperation` and has no WPF control, clipboard, localization, or sample app dependency.
- Modify `src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs`: add narrow public methods for paragraph operations, apply returned text edits through the existing single-undo-unit `ApplyTextOperation`, and keep `Markdown` synchronized.
- Modify `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs`: replace paragraph menu handlers with thin calls to `MarkdownEditor`; add handlers for H4-H6, paragraph, insert paragraph above/below; reuse existing code block/table/horizontal rule handlers.
- Modify `samples/WpfMarkdownEditor.Sample/MainWindow.xaml`: expand the Paragraph popup with approved first-version items and shortcut labels; duplicate Code Block, Table, and Horizontal Rule in Paragraph while preserving Insert menu entries.
- Modify `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`: add English and Chinese strings for Heading 4-6, Paragraph style, and Insert Paragraph Above/Below.
- Modify `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`: add matching XAML resources.
- Modify `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`: add matching XAML resources.
- Create `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownParagraphOperationsTests.cs`: pure operation tests for headings, paragraph cleanup, quote/list toggles, paragraph insertion, and horizontal rule insertion.
- Modify `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs`: add WPF editor-method integration tests for heading, paragraph cleanup, and horizontal rule application.
- Add `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/ParagraphMenuShapeTests.cs`: static XAML tests for first-version Paragraph menu contents and deferred screenshot item absence.
- Modify `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs`: verify new localization keys exist in both languages.

## Implementation Contract

`MarkdownParagraphOperations` must use these public signatures:

```csharp
namespace WpfMarkdownEditor.Wpf.Controls;

internal static class MarkdownParagraphOperations
{
    public static TextEditOperation SetHeadingLevel(string text, int selectionStart, int selectionLength, int level);
    public static TextEditOperation ClearBlockPrefix(string text, int selectionStart, int selectionLength);
    public static TextEditOperation ToggleBlockquote(string text, int selectionStart, int selectionLength);
    public static TextEditOperation ToggleOrderedList(string text, int selectionStart, int selectionLength);
    public static TextEditOperation ToggleBulletList(string text, int selectionStart, int selectionLength);
    public static TextEditOperation InsertParagraphAbove(string text, int selectionStart, int selectionLength);
    public static TextEditOperation InsertParagraphBelow(string text, int selectionStart, int selectionLength);
    public static TextEditOperation InsertHorizontalRule(string text, int selectionStart, int selectionLength);
}
```

`MarkdownEditor` must expose these public methods for the sample app:

```csharp
public void SetHeadingLevel(int level);
public void ClearParagraphStyle();
public void ToggleBlockquote();
public void ToggleOrderedList();
public void ToggleBulletList();
public void InsertParagraphAbove();
public void InsertParagraphBelow();
public void InsertHorizontalRule();
```

Selection semantics:

- Current-line operations affect the current line when `selectionLength == 0`.
- Selected-line operations affect every line touched by the selection.
- When a selection ends exactly at the start of the next line, the next line is not included.
- `InsertParagraphAbove` inserts before the first selected line.
- `InsertParagraphBelow` inserts after the last selected line.
- `InsertHorizontalRule` replaces the selected text and uses blank-line block boundaries.

Supported prefix cleanup for `ClearBlockPrefix`:

```text
# heading
## heading
### heading
#### heading
##### heading
###### heading
> quote
- bullet
* bullet
+ bullet
1. ordered
23. ordered
```

## Task 1: Pure Paragraph Operation Tests

**Files:**
- Create: `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownParagraphOperationsTests.cs`

- [ ] **Step 1: Write the failing test file**

Create `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownParagraphOperationsTests.cs` with:

```csharp
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownParagraphOperationsTests
{
    [Fact]
    public void SetHeadingLevel_CurrentLine_AddsRequestedHeadingMarker()
    {
        var result = MarkdownParagraphOperations.SetHeadingLevel("one\ntwo", 4, 0, 4);

        Assert.Equal("one\n#### two", result.Text);
        Assert.Equal(9, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void SetHeadingLevel_ExistingHeading_ReplacesHeadingMarker()
    {
        var result = MarkdownParagraphOperations.SetHeadingLevel("## title", 3, 0, 5);

        Assert.Equal("##### title", result.Text);
        Assert.Equal(6, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void SetHeadingLevel_SelectedLines_UpdatesEachSelectedLine()
    {
        var result = MarkdownParagraphOperations.SetHeadingLevel("one\ntwo\nthree", 0, "one\ntwo".Length, 2);

        Assert.Equal("## one\n## two\nthree", result.Text);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal("## one\n## two".Length, result.SelectionLength);
    }

    [Fact]
    public void ClearBlockPrefix_HeadingLine_RemovesHeadingMarker()
    {
        var result = MarkdownParagraphOperations.ClearBlockPrefix("### title", 5, 0);

        Assert.Equal("title", result.Text);
        Assert.Equal(2, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ClearBlockPrefix_BlockquoteLine_RemovesQuoteMarker()
    {
        var result = MarkdownParagraphOperations.ClearBlockPrefix("> quote", 3, 0);

        Assert.Equal("quote", result.Text);
        Assert.Equal(1, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Theory]
    [InlineData("- item")]
    [InlineData("* item")]
    [InlineData("+ item")]
    [InlineData("12. item")]
    public void ClearBlockPrefix_ListLine_RemovesListMarker(string input)
    {
        var result = MarkdownParagraphOperations.ClearBlockPrefix(input, input.Length, 0);

        Assert.Equal("item", result.Text);
        Assert.Equal("item".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleBlockquote_CurrentLine_TogglesPrefix()
    {
        var added = MarkdownParagraphOperations.ToggleBlockquote("one", 1, 0);
        var removed = MarkdownParagraphOperations.ToggleBlockquote(added.Text, 3, 0);

        Assert.Equal("> one", added.Text);
        Assert.Equal("one", removed.Text);
    }

    [Fact]
    public void ToggleBulletList_CurrentLine_TogglesPrefix()
    {
        var added = MarkdownParagraphOperations.ToggleBulletList("one", 1, 0);
        var removed = MarkdownParagraphOperations.ToggleBulletList(added.Text, 3, 0);

        Assert.Equal("- one", added.Text);
        Assert.Equal("one", removed.Text);
    }

    [Fact]
    public void ToggleOrderedList_CurrentLine_TogglesPrefix()
    {
        var added = MarkdownParagraphOperations.ToggleOrderedList("one", 1, 0);
        var removed = MarkdownParagraphOperations.ToggleOrderedList(added.Text, 4, 0);

        Assert.Equal("1. one", added.Text);
        Assert.Equal("one", removed.Text);
    }

    [Fact]
    public void InsertParagraphAbove_CurrentLine_InsertsBlankParagraphBeforeLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphAbove("one\ntwo", 5, 0);

        Assert.Equal("one\n\ntwo", result.Text);
        Assert.Equal("one\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertParagraphBelow_CurrentLine_InsertsBlankParagraphAfterLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphBelow("one\ntwo", 1, 0);

        Assert.Equal("one\n\ntwo", result.Text);
        Assert.Equal("one\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertParagraphAbove_SelectedLines_InsertsBeforeFirstSelectedLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphAbove("one\ntwo\nthree", 4, "two\nthree".Length);

        Assert.Equal("one\n\ntwo\nthree", result.Text);
        Assert.Equal("one\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertParagraphBelow_SelectedLines_InsertsAfterLastSelectedLine()
    {
        var result = MarkdownParagraphOperations.InsertParagraphBelow("one\ntwo\nthree", 0, "one\ntwo".Length);

        Assert.Equal("one\ntwo\n\nthree", result.Text);
        Assert.Equal("one\ntwo\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertHorizontalRule_BetweenText_AddsBlankLineBoundaries()
    {
        var result = MarkdownParagraphOperations.InsertHorizontalRule("one\ntwo", 4, 0);

        Assert.Equal("one\n\n---\n\ntwo", result.Text);
        Assert.Equal("one\n\n---\n\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void InsertHorizontalRule_ReplacesSelection()
    {
        var result = MarkdownParagraphOperations.InsertHorizontalRule("one selected two", 4, "selected".Length);

        Assert.Equal("one \n\n---\n\n two", result.Text);
        Assert.Equal("one \n\n---\n\n".Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }
}
```

- [ ] **Step 2: Run tests and verify they fail because the helper is missing**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter MarkdownParagraphOperationsTests --no-restore
```

Expected: build fails with `CS0103` or `CS0246` for `MarkdownParagraphOperations`.

## Task 2: Pure Paragraph Operation Implementation

**Files:**
- Create: `src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs`
- Test: `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownParagraphOperationsTests.cs`

- [ ] **Step 1: Add `MarkdownParagraphOperations` with line parsing and block transforms**

Create `src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs` with this structure:

```csharp
using System.Text.RegularExpressions;

namespace WpfMarkdownEditor.Wpf.Controls;

internal static class MarkdownParagraphOperations
{
    private static readonly Regex BlockPrefixPattern = new(
        @"^(?<prefix>#{1,6}|>|[-*+]|\d+\.)\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextEditOperation SetHeadingLevel(string text, int selectionStart, int selectionLength, int level)
    {
        if (level < 1 || level > 6)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Heading level must be between 1 and 6.");

        return TransformSelectedLines(
            text,
            selectionStart,
            selectionLength,
            line => AddPrefix(RemoveBlockPrefix(line), new string('#', level)));
    }

    public static TextEditOperation ClearBlockPrefix(string text, int selectionStart, int selectionLength) =>
        TransformSelectedLines(text, selectionStart, selectionLength, RemoveBlockPrefix);

    public static TextEditOperation ToggleBlockquote(string text, int selectionStart, int selectionLength) =>
        TogglePrefix(text, selectionStart, selectionLength, ">");

    public static TextEditOperation ToggleOrderedList(string text, int selectionStart, int selectionLength) =>
        TogglePrefix(text, selectionStart, selectionLength, "1.");

    public static TextEditOperation ToggleBulletList(string text, int selectionStart, int selectionLength) =>
        TogglePrefix(text, selectionStart, selectionLength, "-");

    public static TextEditOperation InsertParagraphAbove(string text, int selectionStart, int selectionLength)
    {
        var document = ParagraphLineDocument.Parse(text);
        var range = document.GetSelectedLineRange(selectionStart, selectionLength);
        var insertion = document.Newline;
        var insertAt = document.GetLineStart(range.StartLine);
        var updated = text.Insert(insertAt, insertion);
        return new TextEditOperation(updated, insertAt, 0);
    }

    public static TextEditOperation InsertParagraphBelow(string text, int selectionStart, int selectionLength)
    {
        var document = ParagraphLineDocument.Parse(text);
        var range = document.GetSelectedLineRange(selectionStart, selectionLength);
        var insertAt = document.GetLineEnd(range.EndLine);
        var insertion = document.Newline;
        var updated = text.Insert(insertAt, insertion);
        return new TextEditOperation(updated, insertAt + document.Newline.Length, 0);
    }

    public static TextEditOperation InsertHorizontalRule(string text, int selectionStart, int selectionLength)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);
        var newline = DetectNewline(text);
        var before = text[..start];
        var after = text[(start + length)..];
        var prefix = GetBlockPrefix(before, newline);
        var suffix = GetBlockSuffix(after, newline);
        var insertion = prefix + "---" + suffix;
        return EditorTextOperations.InsertText(text, start, length, insertion);
    }

    private static TextEditOperation TogglePrefix(string text, int selectionStart, int selectionLength, string prefix) =>
        TransformSelectedLines(
            text,
            selectionStart,
            selectionLength,
            line =>
            {
                var match = BlockPrefixPattern.Match(line);
                if (match.Success && string.Equals(match.Groups["prefix"].Value, prefix, StringComparison.Ordinal))
                    return line[match.Length..];

                return AddPrefix(RemoveBlockPrefix(line), prefix);
            });

    private static TextEditOperation TransformSelectedLines(
        string text,
        int selectionStart,
        int selectionLength,
        Func<string, string> transform)
    {
        var document = ParagraphLineDocument.Parse(text);
        var range = document.GetSelectedLineRange(selectionStart, selectionLength);
        var lines = document.Lines.Select(line => line.Content).ToList();

        for (var i = range.StartLine; i <= range.EndLine; i++)
            lines[i] = transform(lines[i]);

        var updated = document.BuildText(lines);
        var newSelectionStart = document.GetLineStart(lines, range.StartLine);
        var newSelectionLength = selectionLength == 0
            ? 0
            : document.GetSelectionLength(lines, range.StartLine, range.EndLine);

        if (selectionLength == 0)
        {
            var originalLineStart = document.GetLineStart(range.StartLine);
            var originalColumn = Math.Max(0, Math.Clamp(selectionStart, 0, text.Length) - originalLineStart);
            var originalLine = document.Lines[range.StartLine].Content;
            var updatedLine = lines[range.StartLine];
            var originalPrefix = GetPrefixLength(originalLine);
            var updatedPrefix = GetPrefixLength(updatedLine);
            var adjustedColumn = Math.Clamp(originalColumn + updatedPrefix - originalPrefix, 0, updatedLine.Length);
            newSelectionStart += adjustedColumn;
        }

        return new TextEditOperation(updated, newSelectionStart, newSelectionLength);
    }

    private static string RemoveBlockPrefix(string line)
    {
        var match = BlockPrefixPattern.Match(line);
        return match.Success ? line[match.Length..] : line;
    }

    private static string AddPrefix(string line, string prefix) =>
        string.IsNullOrWhiteSpace(line) ? prefix + " " : prefix + " " + line;

    private static int GetPrefixLength(string line)
    {
        var match = BlockPrefixPattern.Match(line);
        return match.Success ? match.Length : 0;
    }

    private static string DetectNewline(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal))
            return "\r\n";

        return text.Contains('\n', StringComparison.Ordinal)
            ? "\n"
            : Environment.NewLine;
    }

    private static string GetBlockPrefix(string before, string newline)
    {
        if (before.Length == 0)
            return string.Empty;

        if (before.EndsWith(newline + newline, StringComparison.Ordinal))
            return string.Empty;

        if (before.EndsWith(newline, StringComparison.Ordinal))
            return newline;

        return newline + newline;
    }

    private static string GetBlockSuffix(string after, string newline)
    {
        if (after.Length == 0)
            return string.Empty;

        if (after.StartsWith(newline + newline, StringComparison.Ordinal))
            return string.Empty;

        if (after.StartsWith(newline, StringComparison.Ordinal))
            return newline;

        return newline + newline;
    }

    private sealed record LineRange(int StartLine, int EndLine);
    private sealed record LineInfo(string Content, int Start);

    private sealed class ParagraphLineDocument
    {
        private ParagraphLineDocument(IReadOnlyList<LineInfo> lines, string newline, bool hasTrailingNewline)
        {
            Lines = lines;
            Newline = newline;
            HasTrailingNewline = hasTrailingNewline;
        }

        public IReadOnlyList<LineInfo> Lines { get; }
        public string Newline { get; }
        private bool HasTrailingNewline { get; }

        public static ParagraphLineDocument Parse(string text)
        {
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = new List<LineInfo>();
            var start = 0;
            var index = 0;

            while (index < text.Length)
            {
                if (text[index] == '\r' || text[index] == '\n')
                {
                    lines.Add(new LineInfo(text[start..index], start));
                    if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;

                    index++;
                    start = index;
                    continue;
                }

                index++;
            }

            if (start < text.Length || text.Length == 0)
                lines.Add(new LineInfo(text[start..], start));

            return new ParagraphLineDocument(lines, newline, text.EndsWith("\n", StringComparison.Ordinal));
        }

        public LineRange GetSelectedLineRange(int selectionStart, int selectionLength)
        {
            var start = Math.Clamp(selectionStart, 0, GetOriginalLength());
            var length = Math.Clamp(selectionLength, 0, GetOriginalLength() - start);
            var startLine = GetLineIndex(start, isSelectionEnd: false);
            var endLine = length == 0
                ? startLine
                : GetLineIndex(start + length, isSelectionEnd: true);
            return new LineRange(startLine, endLine);
        }

        public int GetLineIndex(int position, bool isSelectionEnd)
        {
            if (Lines.Count == 0)
                return 0;

            var clamped = Math.Clamp(position, 0, GetOriginalLength());
            for (var i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Start == clamped && isSelectionEnd && clamped > 0)
                    return Math.Max(0, i - 1);

                var nextStart = i + 1 < Lines.Count ? Lines[i + 1].Start : int.MaxValue;
                if (clamped < nextStart)
                    return i;
            }

            return Lines.Count - 1;
        }

        public int GetLineStart(int lineIndex) => GetLineStart(Lines.Select(line => line.Content).ToList(), lineIndex);

        public int GetLineStart(IReadOnlyList<string> lines, int lineIndex)
        {
            var index = Math.Clamp(lineIndex, 0, Math.Max(0, lines.Count - 1));
            var start = 0;
            for (var i = 0; i < index; i++)
                start += lines[i].Length + Newline.Length;

            return start;
        }

        public int GetLineEnd(int lineIndex)
        {
            var index = Math.Clamp(lineIndex, 0, Math.Max(0, Lines.Count - 1));
            return Lines[index].Start + Lines[index].Content.Length;
        }

        public int GetSelectionLength(IReadOnlyList<string> lines, int startLine, int endLine)
        {
            var length = 0;
            for (var i = startLine; i <= endLine; i++)
            {
                if (i > startLine)
                    length += Newline.Length;

                length += lines[i].Length;
            }

            return length;
        }

        public string BuildText(IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
                return string.Empty;

            var text = string.Join(Newline, lines);
            return HasTrailingNewline ? text + Newline : text;
        }

        private int GetOriginalLength()
        {
            if (Lines.Count == 0)
                return 0;

            var length = Lines.Sum(line => line.Content.Length);
            length += Math.Max(0, Lines.Count - 1) * Newline.Length;
            if (HasTrailingNewline)
                length += Newline.Length;

            return length;
        }
    }
}
```

- [ ] **Step 2: Run pure operation tests**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter MarkdownParagraphOperationsTests --no-restore
```

Expected: all `MarkdownParagraphOperationsTests` pass.

- [ ] **Step 3: Commit pure operations**

Run:

```powershell
git add src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownParagraphOperationsTests.cs
git commit -m "Add pure paragraph text operations" -m "Paragraph menu commands need deterministic line-level Markdown edits that stay out of MainWindow, so this adds a WPF-layer helper with direct regression coverage." -m "Constraint: First-version paragraph operations are line-based, not Markdown AST block edits." -m "Confidence: high" -m "Scope-risk: narrow" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter MarkdownParagraphOperationsTests --no-restore"
```

## Task 3: MarkdownEditor Paragraph Methods

**Files:**
- Modify: `src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs`
- Modify: `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs`

- [ ] **Step 1: Add failing WPF editor-method tests**

Append these tests to `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs` before `ClearClipboard()`:

```csharp
[Fact]
public void SetHeadingLevel_UpdatesMarkdownAndSelection()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "one\ntwo";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.CaretIndex = 5;

        editor.SetHeadingLevel(4);

        Assert.Equal("one\n#### two", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal(9, editor.TextBox.CaretIndex);
    });
}

[Fact]
public void ClearParagraphStyle_RemovesSupportedBlockPrefix()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "### title";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.CaretIndex = 5;

        editor.ClearParagraphStyle();

        Assert.Equal("title", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal(2, editor.TextBox.CaretIndex);
    });
}

[Fact]
public void ToggleBulletList_UpdatesSelectedLines()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "one\ntwo\nthree";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.Select(0, "one\ntwo".Length);

        editor.ToggleBulletList();

        Assert.Equal("- one\n- two\nthree", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal("- one\n- two".Length, editor.TextBox.SelectionLength);
    });
}

[Fact]
public void InsertParagraphAbove_InsertsBlankLineAndMovesCaret()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "one\ntwo";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.CaretIndex = 5;

        editor.InsertParagraphAbove();

        Assert.Equal("one\n\ntwo", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal("one\n".Length, editor.TextBox.CaretIndex);
    });
}

[Fact]
public void InsertHorizontalRule_UsesBlockBoundaries()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "one\ntwo";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.CaretIndex = 4;

        editor.InsertHorizontalRule();

        Assert.Equal("one\n\n---\n\ntwo", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal("one\n\n---\n\n".Length, editor.TextBox.CaretIndex);
    });
}
```

- [ ] **Step 2: Run tests and verify they fail because editor methods are missing**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter "SetHeadingLevel_UpdatesMarkdownAndSelection|ClearParagraphStyle_RemovesSupportedBlockPrefix|ToggleBulletList_UpdatesSelectedLines|InsertParagraphAbove_InsertsBlankLineAndMovesCaret|InsertHorizontalRule_UsesBlockBoundaries" --no-restore
```

Expected: build fails with missing methods on `MarkdownEditor`.

- [ ] **Step 3: Add public wrapper methods to `MarkdownEditor.xaml.cs`**

In `src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs`, replace the current `ToggleLinePrefix` method with these public paragraph methods. Keep `ToggleLinePrefix` as a compatibility wrapper for existing callers that are not part of this menu pass.

```csharp
public void SetHeadingLevel(int level) =>
    ApplyParagraphOperation((text, start, length) =>
        MarkdownParagraphOperations.SetHeadingLevel(text, start, length, level));

public void ClearParagraphStyle() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.ClearBlockPrefix);

public void ToggleBlockquote() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.ToggleBlockquote);

public void ToggleOrderedList() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.ToggleOrderedList);

public void ToggleBulletList() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.ToggleBulletList);

public void InsertParagraphAbove() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.InsertParagraphAbove);

public void InsertParagraphBelow() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.InsertParagraphBelow);

public void InsertHorizontalRule() =>
    ApplyParagraphOperation(MarkdownParagraphOperations.InsertHorizontalRule);

public void ToggleLinePrefix(string prefix)
{
    switch (prefix)
    {
        case "#":
            SetHeadingLevel(1);
            break;
        case "##":
            SetHeadingLevel(2);
            break;
        case "###":
            SetHeadingLevel(3);
            break;
        case ">":
            ToggleBlockquote();
            break;
        case "1.":
            ToggleOrderedList();
            break;
        case "-":
            ToggleBulletList();
            break;
        default:
            throw new ArgumentException($"Unsupported line prefix: {prefix}", nameof(prefix));
    }
}

private void ApplyParagraphOperation(Func<string, int, int, TextEditOperation> operationFactory)
{
    var operation = operationFactory(
        EditorTextBox.Text,
        EditorTextBox.SelectionStart,
        EditorTextBox.SelectionLength);
    ApplyTextOperation(operation);
}
```

Place `ApplyParagraphOperation` near the existing private `ApplyTextOperation` helper.

- [ ] **Step 4: Run focused editor-method tests**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter "SetHeadingLevel_UpdatesMarkdownAndSelection|ClearParagraphStyle_RemovesSupportedBlockPrefix|ToggleBulletList_UpdatesSelectedLines|InsertParagraphAbove_InsertsBlankLineAndMovesCaret|InsertHorizontalRule_UsesBlockBoundaries" --no-restore
```

Expected: the five focused WPF tests pass.

- [ ] **Step 5: Commit editor method integration**

Run:

```powershell
git add src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs
git commit -m "Route paragraph edits through MarkdownEditor" -m "The sample menu should not own Markdown line algorithms, so MarkdownEditor now exposes narrow paragraph methods that apply pure text operations through the existing undo-safe path." -m "Constraint: MainWindow remains a click bridge for paragraph operations." -m "Confidence: high" -m "Scope-risk: moderate" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter \"SetHeadingLevel_UpdatesMarkdownAndSelection|ClearParagraphStyle_RemovesSupportedBlockPrefix|ToggleBulletList_UpdatesSelectedLines|InsertParagraphAbove_InsertsBlankLineAndMovesCaret|InsertHorizontalRule_UsesBlockBoundaries\" --no-restore"
```

## Task 4: Localization Keys

**Files:**
- Modify: `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`
- Modify: `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`
- Modify: `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`
- Modify: `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs`

- [ ] **Step 1: Add failing localization key assertions**

Append these inline data rows to `NewFileMenuKeys_ExistInEnglishAndChinese` in `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs`:

```csharp
[InlineData("MainWindow.Heading4")]
[InlineData("MainWindow.Heading5")]
[InlineData("MainWindow.Heading6")]
[InlineData("MainWindow.ParagraphStyle")]
[InlineData("MainWindow.InsertParagraphAbove")]
[InlineData("MainWindow.InsertParagraphBelow")]
```

- [ ] **Step 2: Run localization test and verify failure**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter NewFileMenuKeys_ExistInEnglishAndChinese --no-restore
```

Expected: test fails for the new keys because `LocalizationStrings` does not contain them.

- [ ] **Step 3: Add dictionary keys**

In `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`, add these English entries after `MainWindow.Heading3`:

```csharp
["MainWindow.Heading4"] = "Heading 4",
["MainWindow.Heading5"] = "Heading 5",
["MainWindow.Heading6"] = "Heading 6",
["MainWindow.ParagraphStyle"] = "Paragraph",
["MainWindow.InsertParagraphAbove"] = "Insert Paragraph Above",
["MainWindow.InsertParagraphBelow"] = "Insert Paragraph Below",
```

Add these Chinese entries after `MainWindow.Heading3`:

```csharp
["MainWindow.Heading4"] = "四级标题",
["MainWindow.Heading5"] = "五级标题",
["MainWindow.Heading6"] = "六级标题",
["MainWindow.ParagraphStyle"] = "段落",
["MainWindow.InsertParagraphAbove"] = "在上方插入段落",
["MainWindow.InsertParagraphBelow"] = "在下方插入段落",
```

- [ ] **Step 4: Add XAML resource keys**

In `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`, add after `Loc.MainWindow.Heading3`:

```xml
<sys:String x:Key="Loc.MainWindow.Heading4">Heading 4</sys:String>
<sys:String x:Key="Loc.MainWindow.Heading5">Heading 5</sys:String>
<sys:String x:Key="Loc.MainWindow.Heading6">Heading 6</sys:String>
<sys:String x:Key="Loc.MainWindow.ParagraphStyle">Paragraph</sys:String>
<sys:String x:Key="Loc.MainWindow.InsertParagraphAbove">Insert Paragraph Above</sys:String>
<sys:String x:Key="Loc.MainWindow.InsertParagraphBelow">Insert Paragraph Below</sys:String>
```

In `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`, add after `Loc.MainWindow.Heading3`:

```xml
<sys:String x:Key="Loc.MainWindow.Heading4">四级标题</sys:String>
<sys:String x:Key="Loc.MainWindow.Heading5">五级标题</sys:String>
<sys:String x:Key="Loc.MainWindow.Heading6">六级标题</sys:String>
<sys:String x:Key="Loc.MainWindow.ParagraphStyle">段落</sys:String>
<sys:String x:Key="Loc.MainWindow.InsertParagraphAbove">在上方插入段落</sys:String>
<sys:String x:Key="Loc.MainWindow.InsertParagraphBelow">在下方插入段落</sys:String>
```

- [ ] **Step 5: Run localization test**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter NewFileMenuKeys_ExistInEnglishAndChinese --no-restore
```

Expected: localization key test passes.

- [ ] **Step 6: Commit localization keys**

Run:

```powershell
git add src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs
git commit -m "Add paragraph menu localization keys" -m "Expanded paragraph menu labels need complete English and Chinese resources for both runtime dictionaries and XAML dynamic resources." -m "Constraint: Keep top-menu labels localized through existing MainWindow keys." -m "Confidence: high" -m "Scope-risk: narrow" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter NewFileMenuKeys_ExistInEnglishAndChinese --no-restore"
```

## Task 5: Paragraph Menu Wiring and Shape Tests

**Files:**
- Modify: `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs`
- Modify: `samples/WpfMarkdownEditor.Sample/MainWindow.xaml`
- Create: `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/ParagraphMenuShapeTests.cs`

- [ ] **Step 1: Add failing static menu shape tests**

Create `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/ParagraphMenuShapeTests.cs` with:

```csharp
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class ParagraphMenuShapeTests
{
    [Theory]
    [InlineData("OnHeading1", "Loc.MainWindow.Heading1", "Ctrl+1")]
    [InlineData("OnHeading2", "Loc.MainWindow.Heading2", "Ctrl+2")]
    [InlineData("OnHeading3", "Loc.MainWindow.Heading3", "Ctrl+3")]
    [InlineData("OnHeading4", "Loc.MainWindow.Heading4", "Ctrl+4")]
    [InlineData("OnHeading5", "Loc.MainWindow.Heading5", "Ctrl+5")]
    [InlineData("OnHeading6", "Loc.MainWindow.Heading6", "Ctrl+6")]
    [InlineData("OnParagraphStyle", "Loc.MainWindow.ParagraphStyle", "Ctrl+0")]
    [InlineData("OnQuote", "Loc.MainWindow.Blockquote", "Ctrl+Shift+Q")]
    [InlineData("OnOrderedList", "Loc.MainWindow.OrderedList", "Ctrl+Shift+[")]
    [InlineData("OnUnorderedList", "Loc.MainWindow.BulletList", "Ctrl+Shift+]")]
    [InlineData("OnCodeBlock", "Loc.MainWindow.CodeBlock", "Ctrl+Shift+K")]
    [InlineData("OnTable", "Loc.MainWindow.Table", "")]
    [InlineData("OnHorizontalRule", "Loc.MainWindow.HorizontalRule", "")]
    [InlineData("OnInsertParagraphAbove", "Loc.MainWindow.InsertParagraphAbove", "")]
    [InlineData("OnInsertParagraphBelow", "Loc.MainWindow.InsertParagraphBelow", "")]
    public void ParagraphMenu_ContainsFirstVersionItems(string handler, string resourceKey, string shortcut)
    {
        var paragraphMenu = ExtractParagraphMenuXaml();

        Assert.Contains($"Click=\"{handler}\"", paragraphMenu);
        Assert.Contains(resourceKey, paragraphMenu);
        if (shortcut.Length > 0)
            Assert.Contains($"Tag=\"{shortcut}\"", paragraphMenu);
    }

    [Theory]
    [InlineData("RaiseHeading")]
    [InlineData("LowerHeading")]
    [InlineData("TaskList")]
    [InlineData("TaskStatus")]
    [InlineData("ListIndent")]
    [InlineData("AlertBox")]
    [InlineData("EquationBlock")]
    [InlineData("LinkReference")]
    [InlineData("Footnote")]
    [InlineData("TableOfContents")]
    [InlineData("YamlFrontMatter")]
    public void ParagraphMenu_OmitsDeferredScreenshotItems(string deferredToken)
    {
        var paragraphMenu = ExtractParagraphMenuXaml();

        Assert.DoesNotContain(deferredToken, paragraphMenu);
    }

    [Fact]
    public void InsertMenu_StillContainsExistingBlockInsertionItems()
    {
        var xaml = LoadMainWindowXaml();

        var insertStart = xaml.IndexOf("x:Name=\"InsertPopup\"", StringComparison.Ordinal);
        Assert.True(insertStart >= 0);
        var viewStart = xaml.IndexOf("x:Name=\"ViewMenuBtn\"", insertStart, StringComparison.Ordinal);
        Assert.True(viewStart > insertStart);
        var insertMenu = xaml[insertStart..viewStart];

        Assert.Contains("Click=\"OnCodeBlock\"", insertMenu);
        Assert.Contains("Click=\"OnTable\"", insertMenu);
        Assert.Contains("Click=\"OnHorizontalRule\"", insertMenu);
    }

    private static string ExtractParagraphMenuXaml()
    {
        var xaml = LoadMainWindowXaml();
        var start = xaml.IndexOf("x:Name=\"ParagraphPopup\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("x:Name=\"FormatMenuBtn\"", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return xaml[start..end];
    }

    private static string LoadMainWindowXaml()
    {
        var directory = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "samples",
            "WpfMarkdownEditor.Sample",
            "MainWindow.xaml"));
    }

    private static DirectoryInfo FindRepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        if (directory is not null)
            return directory;

        directory = new DirectoryInfo(Path.GetDirectoryName(sourcePath)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WpfMarkdownEditor.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory;
    }
}
```

- [ ] **Step 2: Run shape tests and verify failure**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter ParagraphMenuShapeTests --no-restore
```

Expected: tests fail because the Paragraph menu does not contain H4-H6, Paragraph, duplicated block insertion items, or insert paragraph above/below.

- [ ] **Step 3: Update `MainWindow.EditorUi.cs` handlers**

In `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs`, update the paragraph handlers to:

```csharp
private void OnHeading1(object sender, RoutedEventArgs e) => Editor.SetHeadingLevel(1);
private void OnHeading2(object sender, RoutedEventArgs e) => Editor.SetHeadingLevel(2);
private void OnHeading3(object sender, RoutedEventArgs e) => Editor.SetHeadingLevel(3);
private void OnHeading4(object sender, RoutedEventArgs e) => Editor.SetHeadingLevel(4);
private void OnHeading5(object sender, RoutedEventArgs e) => Editor.SetHeadingLevel(5);
private void OnHeading6(object sender, RoutedEventArgs e) => Editor.SetHeadingLevel(6);
private void OnParagraphStyle(object sender, RoutedEventArgs e) => Editor.ClearParagraphStyle();
private void OnQuote(object sender, RoutedEventArgs e) => Editor.ToggleBlockquote();
private void OnUnorderedList(object sender, RoutedEventArgs e) => Editor.ToggleBulletList();
private void OnOrderedList(object sender, RoutedEventArgs e) => Editor.ToggleOrderedList();
private void OnInsertParagraphAbove(object sender, RoutedEventArgs e) => Editor.InsertParagraphAbove();
private void OnInsertParagraphBelow(object sender, RoutedEventArgs e) => Editor.InsertParagraphBelow();
private void OnHorizontalRule(object sender, RoutedEventArgs e) => Editor.InsertHorizontalRule();
```

Keep the existing `OnCodeBlock` and `OnTable` handlers in the same file.

- [ ] **Step 4: Expand the Paragraph popup in `MainWindow.xaml`**

Replace the `StackPanel` inside `ParagraphPopup` with:

```xml
<StackPanel MinWidth="220">
    <Button
        Click="OnHeading1"
        Content="{DynamicResource Loc.MainWindow.Heading1}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+1" />
    <Button
        Click="OnHeading2"
        Content="{DynamicResource Loc.MainWindow.Heading2}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+2" />
    <Button
        Click="OnHeading3"
        Content="{DynamicResource Loc.MainWindow.Heading3}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+3" />
    <Button
        Click="OnHeading4"
        Content="{DynamicResource Loc.MainWindow.Heading4}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+4" />
    <Button
        Click="OnHeading5"
        Content="{DynamicResource Loc.MainWindow.Heading5}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+5" />
    <Button
        Click="OnHeading6"
        Content="{DynamicResource Loc.MainWindow.Heading6}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+6" />
    <Button
        Click="OnParagraphStyle"
        Content="{DynamicResource Loc.MainWindow.ParagraphStyle}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+0" />
    <Border Style="{StaticResource MenuSeparatorStyle}" />
    <Button
        Click="OnQuote"
        Content="{DynamicResource Loc.MainWindow.Blockquote}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+Shift+Q" />
    <Button
        Click="OnOrderedList"
        Content="{DynamicResource Loc.MainWindow.OrderedList}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+Shift+[" />
    <Button
        Click="OnUnorderedList"
        Content="{DynamicResource Loc.MainWindow.BulletList}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+Shift+]" />
    <Border Style="{StaticResource MenuSeparatorStyle}" />
    <Button
        Click="OnCodeBlock"
        Content="{DynamicResource Loc.MainWindow.CodeBlock}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+Shift+K" />
    <Button
        Click="OnTable"
        Content="{DynamicResource Loc.MainWindow.Table}"
        Style="{StaticResource MenuItemStyle}" />
    <Button
        Click="OnHorizontalRule"
        Content="{DynamicResource Loc.MainWindow.HorizontalRule}"
        Style="{StaticResource MenuItemStyle}" />
    <Border Style="{StaticResource MenuSeparatorStyle}" />
    <Button
        Click="OnInsertParagraphAbove"
        Content="{DynamicResource Loc.MainWindow.InsertParagraphAbove}"
        Style="{StaticResource MenuItemStyle}" />
    <Button
        Click="OnInsertParagraphBelow"
        Content="{DynamicResource Loc.MainWindow.InsertParagraphBelow}"
        Style="{StaticResource MenuItemStyle}" />
</StackPanel>
```

Do not remove the existing Insert menu buttons for Code Block, Table, or Horizontal Rule.

- [ ] **Step 5: Run shape tests**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter ParagraphMenuShapeTests --no-restore
```

Expected: all `ParagraphMenuShapeTests` pass.

- [ ] **Step 6: Commit menu wiring**

Run:

```powershell
git add samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs samples/WpfMarkdownEditor.Sample/MainWindow.xaml tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/ParagraphMenuShapeTests.cs
git commit -m "Expose usable paragraph menu actions" -m "The Paragraph menu now reflects the approved usable-first scope while preserving existing Insert menu paths for block insertions." -m "Constraint: Deferred screenshot items stay out of the first-version UI." -m "Confidence: high" -m "Scope-risk: moderate" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter ParagraphMenuShapeTests --no-restore"
```

## Task 6: Final Verification and Cleanup

**Files:**
- Review all files changed by Tasks 1-5.

- [ ] **Step 1: Run focused WPF test group**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter "MarkdownParagraphOperationsTests|MarkdownEditorCommandTests|ParagraphMenuShapeTests|NewFileMenuKeys_ExistInEnglishAndChinese" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 2: Run the WPF test project with isolated output if normal output is locked**

First run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --no-restore
```

Expected: test project passes.

When the sample app locks build outputs, run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --no-restore --output D:\tmp\wpf-markdown-viewer-tests
```

Expected: test project passes from isolated output.

- [ ] **Step 3: Run solution-level verification**

Run:

```powershell
dotnet test WpfMarkdownEditor.sln --no-restore
```

Expected: all solution tests pass.

If locked DLLs prevent the solution command from building, close the running sample app and rerun the command. Use isolated output only for the test project because solution-wide `--output` is discouraged by the .NET SDK.

- [ ] **Step 4: Run whitespace check**

Run:

```powershell
git diff --check
```

Expected: no output and exit code `0`.

- [ ] **Step 5: Review final diff**

Run:

```powershell
git status --short --branch
git diff --stat HEAD
git diff -- src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs samples/WpfMarkdownEditor.Sample/MainWindow.xaml
```

Expected:

- No unrelated files are changed.
- `MainWindow.EditorUi.cs` contains only thin handler calls.
- `MainWindow.xaml` contains no deferred screenshot item tokens.
- `MarkdownParagraphOperations.cs` contains no WPF control references.

- [ ] **Step 6: Commit final verification note when additional fixes were needed**

Run this only when Task 6 produced code or test fixes after the previous commits:

```powershell
git add src/WpfMarkdownEditor.Wpf/Controls/MarkdownParagraphOperations.cs src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs samples/WpfMarkdownEditor.Sample/MainWindow.xaml src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownParagraphOperationsTests.cs tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/ParagraphMenuShapeTests.cs tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs
git commit -m "Stabilize paragraph menu verification" -m "Focused verification found final integration issues after the menu and editor layers were wired, so this commit keeps the delivered behavior consistent with the approved scope." -m "Confidence: high" -m "Scope-risk: narrow" -m "Tested: dotnet test WpfMarkdownEditor.sln --no-restore; git diff --check"
```

When Task 6 produces no file changes, do not create an empty commit.

## Coverage Matrix

| Spec requirement | Plan coverage |
| --- | --- |
| H1-H6 available | Tasks 1, 3, 5 |
| Paragraph clears supported block prefixes | Tasks 1, 3, 5 |
| Quote, ordered list, bullet list available | Tasks 1, 3, 5 |
| Code Block, Table, Horizontal Rule duplicated into Paragraph | Task 5 |
| Insert Paragraph Above/Below line-based semantics | Tasks 1, 3, 5 |
| Existing Insert menu paths preserved | Task 5 |
| Deferred screenshot items absent | Task 5 |
| MainWindow remains thin | Tasks 3, 5, 6 |
| Text algorithms live in WPF-layer helper | Tasks 1, 2, 6 |
| One undo unit and Markdown synchronization | Task 3 |
| English and Chinese localization complete | Task 4 |
| Relevant tests pass | Task 6 |
