# Format Menu Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the sample app's top Format menu with stable, executable inline formatting actions.

**Architecture:** Keep `MainWindow.xaml` as the menu layout owner and `MainWindow.EditorUi.cs` as a thin click-to-editor bridge. Reuse `MarkdownEditor.WrapSelection` for wrapper actions, add `MarkdownInlineFormatOperations` for deterministic Clear Style behavior, and expose `MarkdownEditor.ClearInlineStyle()` to apply the pure operation through the existing undo-safe text edit path.

**Tech Stack:** C#/.NET 8 WPF, xUnit, existing `WpfTestHost`, existing localization dictionaries and XAML resource dictionaries.

---

## File Structure

- Create `src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs`: pure static helper that computes Clear Style edits from text plus selection state. It returns existing `TextEditOperation?` and has no WPF control, clipboard, localization, or sample app dependency.
- Modify `src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs`: add `ClearInlineStyle()` and apply returned text edits through existing `ApplyTextOperation`.
- Modify `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs`: add thin handlers for Underline, Comment, and Clear Style; keep existing Bold, Italic, Strikethrough, Inline Code, and Link handlers.
- Modify `samples/WpfMarkdownEditor.Sample/MainWindow.xaml`: expand the Format popup with approved first-version items and shortcut labels; duplicate Hyperlink in Format while preserving Insert menu Link.
- Modify `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`: add English and Chinese strings for Underline, Comment, Hyperlink, and Clear Style.
- Modify `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`: add matching XAML resources.
- Modify `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`: add matching XAML resources.
- Create `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownInlineFormatOperationsTests.cs`: pure operation tests for Clear Style behavior.
- Modify `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs`: add WPF editor-method integration tests for Clear Style and underline wrapping.
- Add `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FormatMenuShapeTests.cs`: static XAML tests for first-version Format menu contents and deferred submenu absence.
- Modify `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs`: verify new localization keys exist in both languages.

## Implementation Contract

`MarkdownInlineFormatOperations` must use this signature:

```csharp
namespace WpfMarkdownEditor.Wpf.Controls;

internal static class MarkdownInlineFormatOperations
{
    public static TextEditOperation? ClearInlineStyle(string text, int selectionStart, int selectionLength);
}
```

`MarkdownEditor` must expose:

```csharp
public void ClearInlineStyle();
```

Clear Style semantics:

- No selection returns `null` from the pure helper.
- No selection leaves editor text unchanged in `MarkdownEditor.ClearInlineStyle()`.
- The operation only inspects the selected text.
- The operation does not scan before or after the selection.
- The operation removes supported wrappers only when each wrapper is complete inside the selection.
- Markdown links keep display text only: `[text](url)` becomes `text`; the URL is intentionally discarded.
- Independent mixed wrappers are cleaned in one pass: `**bold** and *italic*` becomes `bold and italic`.
- Nested wrappers are returned unchanged: `**_text_**` stays `**_text_**`.
- Partial wrappers are returned unchanged: `text**` stays `text**`.
- Plain selected text with no wrapper is returned unchanged.

Simple wrapper semantics:

- Bold, Italic, Underline, Inline Code, Strikethrough, Comment, and Hyperlink use `MarkdownEditor.WrapSelection`.
- These actions wrap; they do not toggle.
- Selecting `**text**` and clicking Bold may produce `****text****` in the first version.
- Underline uses `WrapSelection("<u>", "</u>")`.
- Comment uses `WrapSelection("<!-- ", " -->")`.
- Hyperlink reuses `WrapSelection("[", "](url)")`.
- Shortcut labels are display labels only. Do not add new real key bindings in this plan.

## Task 1: Pure Clear Style Tests

**Files:**
- Create: `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownInlineFormatOperationsTests.cs`

- [ ] **Step 1: Write the failing pure operation tests**

Create `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownInlineFormatOperationsTests.cs` with:

```csharp
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownInlineFormatOperationsTests
{
    [Fact]
    public void ClearInlineStyle_NoSelection_ReturnsNull()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("**bold**", 0, 0);

        Assert.Null(result);
    }

    [Fact]
    public void ClearInlineStyle_BoldSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("**bold**", 0, "**bold**".Length);

        Assert.NotNull(result);
        Assert.Equal("bold", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("bold".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_ItalicSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("*italic*", 0, "*italic*".Length);

        Assert.NotNull(result);
        Assert.Equal("italic", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("italic".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_StrikethroughSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("~~gone~~", 0, "~~gone~~".Length);

        Assert.NotNull(result);
        Assert.Equal("gone", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("gone".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_CodeSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("`code`", 0, "`code`".Length);

        Assert.NotNull(result);
        Assert.Equal("code", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("code".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_UnderlineSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("<u>under</u>", 0, "<u>under</u>".Length);

        Assert.NotNull(result);
        Assert.Equal("under", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("under".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_CommentSelection_RemovesWrapper()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("<!-- note -->", 0, "<!-- note -->".Length);

        Assert.NotNull(result);
        Assert.Equal("note", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("note".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_LinkSelection_KeepsLinkText()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("[label](https://example.com)", 0, "[label](https://example.com)".Length);

        Assert.NotNull(result);
        Assert.Equal("label", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("label".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_MixedSelection_RemovesSupportedWrappers()
    {
        var input = "**bold** and *italic* and `code`";
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(input, 0, input.Length);

        Assert.NotNull(result);
        Assert.Equal("bold and italic and code", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("bold and italic and code".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_SelectionWithNoWrapper_ReturnsUnchangedText()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("plain text", 0, "plain text".Length);

        Assert.NotNull(result);
        Assert.Equal("plain text", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("plain text".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_NestedWrapper_ReturnsUnchangedText()
    {
        var result = MarkdownInlineFormatOperations.ClearInlineStyle("**_text_**", 0, "**_text_**".Length);

        Assert.NotNull(result);
        Assert.Equal("**_text_**", result.Value.Text);
        Assert.Equal(0, result.Value.SelectionStart);
        Assert.Equal("**_text_**".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_PartialWrapper_ReturnsUnchangedText()
    {
        var input = "**text**";
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(input, 2, "text**".Length);

        Assert.NotNull(result);
        Assert.Equal(input, result.Value.Text);
        Assert.Equal(2, result.Value.SelectionStart);
        Assert.Equal("text**".Length, result.Value.SelectionLength);
    }

    [Fact]
    public void ClearInlineStyle_SelectionInsideDocument_ReplacesOnlySelection()
    {
        var input = "before **bold** after";
        var start = "before ".Length;
        var length = "**bold**".Length;
        var result = MarkdownInlineFormatOperations.ClearInlineStyle(input, start, length);

        Assert.NotNull(result);
        Assert.Equal("before bold after", result.Value.Text);
        Assert.Equal(start, result.Value.SelectionStart);
        Assert.Equal("bold".Length, result.Value.SelectionLength);
    }
}
```

- [ ] **Step 2: Run tests and verify they fail because the helper is missing**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter MarkdownInlineFormatOperationsTests --no-restore
```

Expected: build fails with `CS0103` or `CS0246` for `MarkdownInlineFormatOperations`.

## Task 2: Pure Clear Style Implementation

**Files:**
- Create: `src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs`
- Test: `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownInlineFormatOperationsTests.cs`

- [ ] **Step 1: Add `MarkdownInlineFormatOperations`**

Create `src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs` with:

```csharp
using System.Text.RegularExpressions;

namespace WpfMarkdownEditor.Wpf.Controls;

internal static class MarkdownInlineFormatOperations
{
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[(?<text>[^\[\]]+)\]\((?<url>[^()]*)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TextEditOperation? ClearInlineStyle(string text, int selectionStart, int selectionLength)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);
        if (length == 0)
            return null;

        var selection = text.Substring(start, length);
        var cleaned = ClearSelection(selection);
        var updated = text.Remove(start, length).Insert(start, cleaned);
        return new TextEditOperation(updated, start, cleaned.Length);
    }

    private static string ClearSelection(string selection)
    {
        if (ContainsNestedWrapper(selection) || HasPartialWrapper(selection))
            return selection;

        var cleaned = selection;
        cleaned = ReplaceWrappedSegments(cleaned, "**", "**");
        cleaned = ReplaceWrappedSegments(cleaned, "~~", "~~");
        cleaned = ReplaceWrappedSegments(cleaned, "`", "`");
        cleaned = ReplaceWrappedSegments(cleaned, "<u>", "</u>");
        cleaned = ReplaceWrappedSegments(cleaned, "<!-- ", " -->", static inner => inner.Trim());
        cleaned = MarkdownLinkPattern.Replace(cleaned, match => match.Groups["text"].Value);
        cleaned = ReplaceWrappedSegments(cleaned, "*", "*");
        return cleaned;
    }

    private static string ReplaceWrappedSegments(
        string text,
        string opening,
        string closing,
        Func<string, string>? transformInner = null)
    {
        var result = text;
        var searchStart = 0;

        while (searchStart < result.Length)
        {
            var openIndex = result.IndexOf(opening, searchStart, StringComparison.Ordinal);
            if (openIndex < 0)
                break;

            var contentStart = openIndex + opening.Length;
            var closeIndex = result.IndexOf(closing, contentStart, StringComparison.Ordinal);
            if (closeIndex < 0)
                break;

            var inner = result[contentStart..closeIndex];
            if (inner.Length == 0)
            {
                searchStart = closeIndex + closing.Length;
                continue;
            }

            var replacement = transformInner is null ? inner : transformInner(inner);
            result = result[..openIndex] + replacement + result[(closeIndex + closing.Length)..];
            searchStart = openIndex + replacement.Length;
        }

        return result;
    }

    private static bool ContainsNestedWrapper(string selection)
    {
        return IsSingleWrapped(selection, "**", "**", out var boldInner) && ContainsAnyWrapper(boldInner)
            || IsSingleWrapped(selection, "*", "*", out var italicInner) && ContainsAnyWrapper(italicInner)
            || IsSingleWrapped(selection, "~~", "~~", out var strikeInner) && ContainsAnyWrapper(strikeInner)
            || IsSingleWrapped(selection, "`", "`", out var codeInner) && ContainsAnyWrapper(codeInner)
            || IsSingleWrapped(selection, "<u>", "</u>", out var underlineInner) && ContainsAnyWrapper(underlineInner)
            || IsSingleWrapped(selection, "<!-- ", " -->", out var commentInner) && ContainsAnyWrapper(commentInner);
    }

    private static bool IsSingleWrapped(string selection, string opening, string closing, out string inner)
    {
        inner = string.Empty;
        if (!selection.StartsWith(opening, StringComparison.Ordinal) ||
            !selection.EndsWith(closing, StringComparison.Ordinal) ||
            selection.Length <= opening.Length + closing.Length)
        {
            return false;
        }

        inner = selection[opening.Length..^closing.Length];
        return true;
    }

    private static bool ContainsAnyWrapper(string text)
    {
        return text.Contains("**", StringComparison.Ordinal)
            || text.Contains("~~", StringComparison.Ordinal)
            || text.Contains('`', StringComparison.Ordinal)
            || text.Contains("<u>", StringComparison.Ordinal)
            || text.Contains("</u>", StringComparison.Ordinal)
            || text.Contains("<!--", StringComparison.Ordinal)
            || text.Contains("-->", StringComparison.Ordinal)
            || MarkdownLinkPattern.IsMatch(text)
            || HasStandaloneItalicMarker(text);
    }

    private static bool HasStandaloneItalicMarker(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '*')
                continue;

            var previousIsStar = i > 0 && text[i - 1] == '*';
            var nextIsStar = i + 1 < text.Length && text[i + 1] == '*';
            if (!previousIsStar && !nextIsStar)
                return true;
        }

        return false;
    }

    private static bool HasPartialWrapper(string selection)
    {
        return HasUnmatchedToken(selection, "**")
            || HasUnmatchedToken(selection, "~~")
            || HasUnmatchedToken(selection, "`")
            || HasUnmatchedPair(selection, "<u>", "</u>")
            || HasUnmatchedPair(selection, "<!-- ", " -->")
            || HasPartialLink(selection)
            || HasUnmatchedStandaloneItalic(selection);
    }

    private static bool HasUnmatchedToken(string selection, string token) =>
        CountOccurrences(selection, token) % 2 != 0;

    private static bool HasUnmatchedPair(string selection, string opening, string closing) =>
        CountOccurrences(selection, opening) != CountOccurrences(selection, closing);

    private static bool HasPartialLink(string selection)
    {
        return CountOccurrences(selection, "[") != CountOccurrences(selection, "]")
            || CountOccurrences(selection, "(") != CountOccurrences(selection, ")");
    }

    private static bool HasUnmatchedStandaloneItalic(string selection)
    {
        var count = 0;
        for (var i = 0; i < selection.Length; i++)
        {
            if (selection[i] != '*')
                continue;

            var previousIsStar = i > 0 && selection[i - 1] == '*';
            var nextIsStar = i + 1 < selection.Length && selection[i + 1] == '*';
            if (!previousIsStar && !nextIsStar)
                count++;
        }

        return count % 2 != 0;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
                break;

            count++;
            index = found + value.Length;
        }

        return count;
    }
}
```

- [ ] **Step 2: Run pure operation tests**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter MarkdownInlineFormatOperationsTests --no-restore
```

Expected: all `MarkdownInlineFormatOperationsTests` pass.

- [ ] **Step 3: Commit pure inline operations**

Run:

```powershell
git add src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownInlineFormatOperationsTests.cs
git commit -m "Add pure inline format cleanup" -m "Clear Style needs deterministic selection-only wrapper cleanup without adding parsing logic to MainWindow, so this adds a WPF-layer helper with direct tests for URL, mixed, nested, and partial-wrapper semantics." -m "Constraint: First-version clear style is selection-only and does not scan outside the selected text." -m "Confidence: high" -m "Scope-risk: narrow" -m "Directive: Do not add nested or partial inline cleanup without a separate Phase 2 design." -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter MarkdownInlineFormatOperationsTests --no-restore"
```

## Task 3: MarkdownEditor Clear Style Integration

**Files:**
- Modify: `src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs`
- Modify: `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs`

- [ ] **Step 1: Add failing WPF editor integration tests**

Append these tests to `tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs` before `ClearClipboard()`:

```csharp
[Fact]
public void ClearInlineStyle_Selection_UpdatesMarkdown()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "before **bold** after";
        editor.Markdown = editor.TextBox.Text;
        var start = "before ".Length;
        editor.TextBox.Select(start, "**bold**".Length);

        editor.ClearInlineStyle();

        Assert.Equal("before bold after", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal(start, editor.TextBox.SelectionStart);
        Assert.Equal("bold".Length, editor.TextBox.SelectionLength);
    });
}

[Fact]
public void ClearInlineStyle_NoSelection_DoesNotChangeText()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "**bold**";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.CaretIndex = 2;

        editor.ClearInlineStyle();

        Assert.Equal("**bold**", editor.TextBox.Text);
        Assert.Equal(editor.TextBox.Text, editor.Markdown);
        Assert.Equal(2, editor.TextBox.CaretIndex);
    });
}

[Fact]
public void WrapSelection_UnderlineMarkers_UsesHtmlUnderline()
{
    WpfTestHost.Run(() =>
    {
        using var editor = new MarkdownEditor();
        editor.TextBox.Text = "under";
        editor.Markdown = editor.TextBox.Text;
        editor.TextBox.Select(0, "under".Length);

        editor.WrapSelection("<u>", "</u>");

        Assert.Equal("<u>under</u>", editor.TextBox.Text);
        Assert.Equal("under".Length, editor.TextBox.SelectionLength);
    });
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter "ClearInlineStyle_Selection_UpdatesMarkdown|ClearInlineStyle_NoSelection_DoesNotChangeText|WrapSelection_UnderlineMarkers_UsesHtmlUnderline" --no-restore
```

Expected: build fails for missing `MarkdownEditor.ClearInlineStyle()`.

- [ ] **Step 3: Add `ClearInlineStyle()` to `MarkdownEditor`**

In `src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs`, add this public method near the other public editing helpers:

```csharp
public void ClearInlineStyle()
{
    var operation = MarkdownInlineFormatOperations.ClearInlineStyle(
        EditorTextBox.Text,
        EditorTextBox.SelectionStart,
        EditorTextBox.SelectionLength);
    if (operation is null)
    {
        EditorTextBox.Focus();
        return;
    }

    ApplyTextOperation(operation.Value);
}
```

This method must use `ApplyTextOperation` so the edit stays in one undo unit and updates `Markdown`.

- [ ] **Step 4: Run focused editor tests**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter "ClearInlineStyle_Selection_UpdatesMarkdown|ClearInlineStyle_NoSelection_DoesNotChangeText|WrapSelection_UnderlineMarkers_UsesHtmlUnderline" --no-restore
```

Expected: the three focused WPF tests pass.

- [ ] **Step 5: Commit editor integration**

Run:

```powershell
git add src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs
git commit -m "Route clear style through MarkdownEditor" -m "The Format menu needs a reusable editor-level Clear Style operation, so MarkdownEditor now applies the pure inline cleanup helper through the existing undo-safe text operation path." -m "Constraint: Clear Style with no selection remains a no-op that refocuses the editor." -m "Confidence: high" -m "Scope-risk: moderate" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter \"ClearInlineStyle_Selection_UpdatesMarkdown|ClearInlineStyle_NoSelection_DoesNotChangeText|WrapSelection_UnderlineMarkers_UsesHtmlUnderline\" --no-restore"
```

## Task 4: Format Localization Keys

**Files:**
- Modify: `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`
- Modify: `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`
- Modify: `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`
- Modify: `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs`

- [ ] **Step 1: Add failing localization key assertions**

Append these inline data rows to `NewFileMenuKeys_ExistInEnglishAndChinese` in `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs`:

```csharp
[InlineData("MainWindow.Underline")]
[InlineData("MainWindow.Comment")]
[InlineData("MainWindow.Hyperlink")]
[InlineData("MainWindow.ClearStyle")]
```

- [ ] **Step 2: Run localization test and verify failure**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter NewFileMenuKeys_ExistInEnglishAndChinese --no-restore
```

Expected: test fails for the four new keys because `LocalizationStrings` does not contain them.

- [ ] **Step 3: Add dictionary keys**

In `src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs`, add these English entries near the existing Format keys:

```csharp
["MainWindow.Underline"] = "Underline",
["MainWindow.Comment"] = "Comment",
["MainWindow.Hyperlink"] = "Hyperlink",
["MainWindow.ClearStyle"] = "Clear Style",
```

Add these Chinese entries near the existing Format keys:

```csharp
["MainWindow.Underline"] = "下划线",
["MainWindow.Comment"] = "注释",
["MainWindow.Hyperlink"] = "超链接",
["MainWindow.ClearStyle"] = "清除样式",
```

- [ ] **Step 4: Add XAML resource keys**

In `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml`, add near the existing Format keys:

```xml
<sys:String x:Key="Loc.MainWindow.Underline">Underline</sys:String>
<sys:String x:Key="Loc.MainWindow.Comment">Comment</sys:String>
<sys:String x:Key="Loc.MainWindow.Hyperlink">Hyperlink</sys:String>
<sys:String x:Key="Loc.MainWindow.ClearStyle">Clear Style</sys:String>
```

In `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml`, add near the existing Format keys:

```xml
<sys:String x:Key="Loc.MainWindow.Underline">下划线</sys:String>
<sys:String x:Key="Loc.MainWindow.Comment">注释</sys:String>
<sys:String x:Key="Loc.MainWindow.Hyperlink">超链接</sys:String>
<sys:String x:Key="Loc.MainWindow.ClearStyle">清除样式</sys:String>
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
git commit -m "Add format menu localization keys" -m "Expanded Format menu labels need complete English and Chinese resources for both runtime dictionaries and XAML dynamic resources." -m "Constraint: Hyperlink intentionally uses a separate top Format menu label from the existing Insert Link label." -m "Confidence: high" -m "Scope-risk: narrow" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter NewFileMenuKeys_ExistInEnglishAndChinese --no-restore"
```

## Task 5: Format Menu Wiring and Shape Tests

**Files:**
- Modify: `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs`
- Modify: `samples/WpfMarkdownEditor.Sample/MainWindow.xaml`
- Create: `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FormatMenuShapeTests.cs`

- [ ] **Step 1: Add failing static menu shape tests**

Create `tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FormatMenuShapeTests.cs` with:

```csharp
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class FormatMenuShapeTests
{
    [Theory]
    [InlineData("OnBold", "Loc.MainWindow.Bold", "Ctrl+B")]
    [InlineData("OnItalic", "Loc.MainWindow.Italic", "Ctrl+I")]
    [InlineData("OnUnderline", "Loc.MainWindow.Underline", "Ctrl+U")]
    [InlineData("OnInlineCode", "Loc.MainWindow.InlineCode", "Ctrl+Shift+`")]
    [InlineData("OnStrikethrough", "Loc.MainWindow.Strikethrough", "Alt+Shift+5")]
    [InlineData("OnComment", "Loc.MainWindow.Comment", "")]
    [InlineData("OnLink", "Loc.MainWindow.Hyperlink", "Ctrl+K")]
    [InlineData("OnClearStyle", "Loc.MainWindow.ClearStyle", "Ctrl+\\")]
    public void FormatMenu_ContainsFirstVersionItems(string handler, string resourceKey, string shortcut)
    {
        var formatMenu = ExtractFormatMenuXaml();

        Assert.Contains($"Click=\"{handler}\"", formatMenu);
        Assert.Contains(resourceKey, formatMenu);
        if (shortcut.Length > 0)
            Assert.Contains($"Tag=\"{shortcut}\"", formatMenu);
    }

    [Theory]
    [InlineData("LinkOperations")]
    [InlineData("ImageOperations")]
    [InlineData("OnLinkOperations")]
    [InlineData("OnImageOperations")]
    public void FormatMenu_OmitsDeferredScreenshotSubmenus(string deferredToken)
    {
        var formatMenu = ExtractFormatMenuXaml();

        Assert.DoesNotContain(deferredToken, formatMenu);
    }

    [Fact]
    public void InsertMenu_StillContainsExistingLinkItem()
    {
        var xaml = LoadMainWindowXaml();

        var insertStart = xaml.IndexOf("x:Name=\"InsertPopup\"", StringComparison.Ordinal);
        Assert.True(insertStart >= 0);
        var viewStart = xaml.IndexOf("x:Name=\"ViewMenuBtn\"", insertStart, StringComparison.Ordinal);
        Assert.True(viewStart > insertStart);
        var insertMenu = xaml[insertStart..viewStart];

        Assert.Contains("Click=\"OnLink\"", insertMenu);
        Assert.Contains("Loc.MainWindow.Link", insertMenu);
    }

    private static string ExtractFormatMenuXaml()
    {
        var xaml = LoadMainWindowXaml();
        var start = xaml.IndexOf("x:Name=\"FormatPopup\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = xaml.IndexOf("x:Name=\"InsertMenuBtn\"", start, StringComparison.Ordinal);
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
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter FormatMenuShapeTests --no-restore
```

Expected: tests fail because the Format menu does not contain Underline, Comment, Hyperlink, Clear Style, or the new shortcut labels.

- [ ] **Step 3: Update `MainWindow.EditorUi.cs` handlers**

In `samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs`, add these handlers near the existing Format handlers:

```csharp
private void OnUnderline(object sender, RoutedEventArgs e) => Editor.WrapSelection("<u>", "</u>");
private void OnComment(object sender, RoutedEventArgs e) => Editor.WrapSelection("<!-- ", " -->");
private void OnClearStyle(object sender, RoutedEventArgs e) => Editor.ClearInlineStyle();
```

Keep the existing `OnLink` handler unchanged so Insert and Format can share it.

- [ ] **Step 4: Expand the Format popup in `MainWindow.xaml`**

Replace the `StackPanel` inside `FormatPopup` with:

```xml
<StackPanel MinWidth="220">
    <Button
        Click="OnBold"
        Content="{DynamicResource Loc.MainWindow.Bold}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+B" />
    <Button
        Click="OnItalic"
        Content="{DynamicResource Loc.MainWindow.Italic}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+I" />
    <Button
        Click="OnUnderline"
        Content="{DynamicResource Loc.MainWindow.Underline}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+U" />
    <Button
        Click="OnInlineCode"
        Content="{DynamicResource Loc.MainWindow.InlineCode}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+Shift+`" />
    <Border Style="{StaticResource MenuSeparatorStyle}" />
    <Button
        Click="OnStrikethrough"
        Content="{DynamicResource Loc.MainWindow.Strikethrough}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Alt+Shift+5" />
    <Button
        Click="OnComment"
        Content="{DynamicResource Loc.MainWindow.Comment}"
        Style="{StaticResource MenuItemStyle}" />
    <Border Style="{StaticResource MenuSeparatorStyle}" />
    <Button
        Click="OnLink"
        Content="{DynamicResource Loc.MainWindow.Hyperlink}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+K" />
    <Border Style="{StaticResource MenuSeparatorStyle}" />
    <Button
        Click="OnClearStyle"
        Content="{DynamicResource Loc.MainWindow.ClearStyle}"
        Style="{StaticResource MenuItemStyle}"
        Tag="Ctrl+\" />
</StackPanel>
```

Do not add disabled Link Operations or Image submenu entries. Do not remove the existing Insert menu Link button.

- [ ] **Step 5: Run shape tests**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter FormatMenuShapeTests --no-restore
```

Expected: all `FormatMenuShapeTests` pass.

- [ ] **Step 6: Commit menu wiring**

Run:

```powershell
git add samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs samples/WpfMarkdownEditor.Sample/MainWindow.xaml tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FormatMenuShapeTests.cs
git commit -m "Expose usable format menu actions" -m "The Format menu now reflects the approved usable-first scope while preserving the existing Insert menu Link path." -m "Constraint: Deferred Link Operations and Image submenus stay out of the first-version UI." -m "Confidence: high" -m "Scope-risk: moderate" -m "Tested: dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter FormatMenuShapeTests --no-restore"
```

## Task 6: Final Verification and Cleanup

**Files:**
- Review all files changed by Tasks 1-5.

- [ ] **Step 1: Run focused WPF test group**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter "MarkdownInlineFormatOperationsTests|MarkdownEditorCommandTests|FormatMenuShapeTests|NewFileMenuKeys_ExistInEnglishAndChinese" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 2: Run the WPF test project**

Run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --no-restore
```

Expected: test project passes.

When the sample app locks build outputs, run:

```powershell
dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --no-restore --output D:\tmp\wpf-markdown-viewer-format-tests
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
git diff -- src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs samples/WpfMarkdownEditor.Sample/MainWindow.xaml
```

Expected:

- No unrelated files are changed.
- `MainWindow.EditorUi.cs` contains only thin handler calls for Format actions.
- `MainWindow.xaml` contains no Link Operations or Image submenu tokens in Format.
- `MarkdownInlineFormatOperations.cs` contains no WPF control references.
- Existing Insert menu Link remains present.

- [ ] **Step 6: Commit final verification fixes when additional fixes were needed**

Run this only when Task 6 produced code or test fixes after the previous commits:

```powershell
git add src/WpfMarkdownEditor.Wpf/Controls/MarkdownInlineFormatOperations.cs src/WpfMarkdownEditor.Wpf/Controls/MarkdownEditor.xaml.cs samples/WpfMarkdownEditor.Sample/MainWindow.EditorUi.cs samples/WpfMarkdownEditor.Sample/MainWindow.xaml src/WpfMarkdownEditor.Wpf/Localization/LocalizationStrings.cs src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownInlineFormatOperationsTests.cs tests/WpfMarkdownEditor.Wpf.Tests/Controls/MarkdownEditorCommandTests.cs tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FormatMenuShapeTests.cs tests/WpfMarkdownEditor.Wpf.Tests/FileMenu/FileMenuLocalizationTests.cs
git commit -m "Stabilize format menu verification" -m "Focused verification found final integration issues after the menu and editor layers were wired, so this commit keeps the delivered behavior consistent with the approved scope." -m "Confidence: high" -m "Scope-risk: narrow" -m "Tested: dotnet test WpfMarkdownEditor.sln --no-restore; git diff --check"
```

When Task 6 produces no file changes, do not create an empty commit.

## Coverage Matrix

| Spec requirement | Plan coverage |
| --- | --- |
| Bold, Italic, Inline Code, Strikethrough present | Task 5 |
| Underline present and uses `<u>` wrappers | Tasks 3, 5 |
| Comment present and uses `<!-- ` / ` -->` template | Task 5 |
| Hyperlink duplicated into Format | Task 5 |
| Existing Insert menu Link preserved | Task 5 |
| Clear Style available | Tasks 1, 2, 3, 5 |
| Link URL discarded by Clear Style | Tasks 1, 2 |
| Mixed independent wrappers cleaned | Tasks 1, 2 |
| Nested and partial wrappers unchanged | Tasks 1, 2 |
| No-selection Clear Style no-op | Tasks 1, 3 |
| MainWindow remains thin | Tasks 3, 5, 6 |
| Inline cleanup lives in WPF helper | Tasks 1, 2, 6 |
| No new real key bindings | Task 5 |
| English and Chinese localization complete | Task 4 |
| Relevant tests pass | Task 6 |
