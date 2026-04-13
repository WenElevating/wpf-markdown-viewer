# WPF Markdown Editor Control - Implementation Plan

**Created:** 2026-04-13
**Status:** Draft
**Target:** .NET 8 + WPF
**Type:** Greenfield

---

## RALPLAN-DR Summary

### Principles (5 Core Principles)

1. **Zero External Dependencies** - Pure .NET 8 implementation, no NuGet packages for core functionality
2. **Separation of Concerns** - Core library (parser/AST) has no WPF dependencies; WPF library handles rendering only
3. **CommonMark Compliance First** - Parser correctness over feature breadth; 90% spec compliance is the gate
4. **Performance by Design** - Debounced async rendering, background parsing, <50ms update latency
5. **Test-Driven Development** - Core library must reach 80% line coverage before WPF integration

### Decision Drivers (Top 3)

| Driver | Impact | Rationale |
|--------|--------|-----------|
| **Embeddability** | High | UserControl design enables integration into any WPF application |
| **CommonMark 90% Pass Rate** | High | Validates parser correctness; requires spec test integration |
| **Zero Dependencies** | High | Simplifies deployment but increases implementation complexity |

### Viable Options Considered

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| **A: Custom Parser (Chosen)** | Zero dependencies, full control, learning opportunity | Higher effort, must handle edge cases | Selected |
| **B: Markdig Integration** | Proven parser, extended features | Violates zero-dependency constraint | Rejected |
| **C: WebView2 + JavaScript** | Rich ecosystem, full CommonMark support | Heavy runtime, not pure WPF, violates constraints | Rejected |

---

## Implementation Phases

### Phase 1: Project Foundation & AST Model
**Duration Estimate:** 1-2 sessions
**Risk Level:** LOW

#### Files to Create

```
WpfMarkdownEditor/
├── WpfMarkdownEditor.sln
├── src/
│   ├── WpfMarkdownEditor.Core/
│   │   ├── WpfMarkdownEditor.Core.csproj
│   │   └── Parsing/
│   │       ├── Block.cs                    # Abstract base class
│   │       ├── Inline.cs                   # Abstract base class
│   │       ├── Blocks/
│   │       │   ├── HeadingBlock.cs
│   │       │   ├── ParagraphBlock.cs
│   │       │   ├── CodeBlock.cs
│   │       │   ├── TableBlock.cs
│   │       │   ├── BlockquoteBlock.cs
│   │       │   ├── ListBlock.cs
│   │       │   ├── ThematicBreakBlock.cs
│   │       │   └── ImageBlock.cs
│   │       └── Inlines/
│   │           ├── TextInline.cs
│   │           ├── BoldInline.cs
│   │           ├── ItalicInline.cs
│   │           ├── BoldItalicInline.cs
│   │           ├── CodeInline.cs
│   │           ├── LinkInline.cs
│   │           └── ImageInline.cs
│   └── WpfMarkdownEditor.Wpf/
│       └── WpfMarkdownEditor.Wpf.csproj
└── tests/
    ├── WpfMarkdownEditor.Core.Tests/
    │   └── WpfMarkdownEditor.Core.Tests.csproj
    └── WpfMarkdownEditor.Wpf.Tests/
        └── WpfMarkdownEditor.Wpf.Tests.csproj
```

#### Acceptance Criteria

- [ ] Solution builds with `dotnet build`
- [ ] `WpfMarkdownEditor.Core.csproj` targets `net8.0`
- [ ] `WpfMarkdownEditor.Wpf.csproj` targets `net8.0-windows`
- [ ] All Block types defined with correct properties (per design spec lines 109-194)
- [ ] All Inline types defined with correct properties (per design spec lines 199-244)
- [ ] Unit tests for AST model compile (empty tests OK at this stage)

---

### Phase 2: Markdown Parser Implementation
**Duration Estimate:** 3-4 sessions
**Risk Level:** HIGH (Core complexity)

#### Files to Create

```
src/WpfMarkdownEditor.Core/Parsing/
├── MarkdownParser.cs           # Main parser entry point
├── ParserState.cs              # State machine for line-by-line processing
├── BlockParser.cs              # Block-level parsing logic
├── InlineParser.cs             # Inline element parsing (delimiter stack algorithm)
├── LineReader.cs               # Line enumeration with position tracking
└── CommonMark/
    ├── CommonMarkSpec.cs       # Spec constants and patterns
    └── HtmlEntityHelper.cs     # HTML entity decoding
src/WpfMarkdownEditor.Core/Rendering/
└── HtmlRenderer.cs             # HTML renderer for CommonMark spec test validation
```

#### Implementation Order

1. **LineReader.cs** - Line enumeration with position tracking
2. **ParserState.cs** - State machine (inCodeBlock, inBlockquote, inList, etc.)
3. **BlockParser.cs** - Identify block boundaries and types
4. **InlineParser.cs** - Delimiter stack algorithm for inline formatting
5. **MarkdownParser.cs** - Orchestration layer
6. **HtmlRenderer.cs** - HTML output for CommonMark spec test validation

#### CommonMark Compliance Strategy

```
CommonMark Spec Tests Integration:
1. Download spec tests from https://spec.commonmark.org/0.31/spec.json
2. Create test adapter to parse JSON test cases
3. Implement test runner that executes all 600+ tests
4. Track pass rate; iterate until 90% achieved
```

#### Acceptance Criteria

- [ ] `MarkdownParser.Parse()` returns `List<Block>` for any input string
- [ ] Handles empty input without exceptions
- [ ] Handles malformed Markdown gracefully
- [ ] `HtmlRenderer.Render()` produces HTML output for spec test validation
- [ ] Block types correctly identified:
  - [ ] ATX Headings (`# H1` through `###### H6`)
  - [ ] Setext Headings (`H1\n===`, `H2\n---`)
  - [ ] Fenced Code Blocks (``` and ~~~)
  - [ ] Indented Code Blocks
  - [ ] HTML Blocks
  - [ ] Blockquotes (including nested)
  - [ ] Ordered Lists
  - [ ] Unordered Lists
  - [ ] Thematic Breaks
  - [ ] Paragraphs
  - [ ] Tables (GFM extension)
- [ ] Inline types correctly parsed:
  - [ ] Bold (`**text**` and `__text__`)
  - [ ] Italic (`*text*` and `_text_`)
  - [ ] Bold+Italic
  - [ ] Inline Code (`` `code` ``)
  - [ ] Links (`[text](url)`)
  - [ ] Images (`![alt](url)`)
  - [ ] HTML Inline tags
- [ ] **CommonMark Spec Tests: 90% pass rate**
- [ ] **Performance baseline established: <50ms for 10KB document parsing**

---

### Phase 3: Core Test Suite & Coverage
**Duration Estimate:** 1-2 sessions
**Risk Level:** MEDIUM

#### Test Categories

| Category | Test Count (Est.) | Priority |
|----------|-------------------|----------|
| Block Parsing | 50+ | High |
| Inline Parsing | 40+ | High |
| Edge Cases | 30+ | Medium |
| CommonMark Spec | 600+ | Critical |
| Performance | 5+ | Medium |

#### Files to Create

```
tests/WpfMarkdownEditor.Core.Tests/
├── Parsing/
│   ├── BlockParserTests.cs
│   ├── InlineParserTests.cs
│   ├── MarkdownParserTests.cs
│   └── CommonMarkSpecTests.cs
├── Blocks/
│   ├── HeadingBlockTests.cs
│   ├── ParagraphBlockTests.cs
│   ├── CodeBlockTests.cs
│   ├── TableBlockTests.cs
│   ├── BlockquoteBlockTests.cs
│   └── ListBlockTests.cs
└── Performance/
    └── ParserPerformanceTests.cs
```

#### Acceptance Criteria

- [ ] All tests pass with `dotnet test`
- [ ] **80% line coverage** on `WpfMarkdownEditor.Core`
- [ ] Coverage report generated via `dotnet coverage`
- [ ] CommonMark spec tests integrated and passing at 90%

---

### Phase 4: WPF Theme System & Rendering
**Duration Estimate:** 2-3 sessions
**Risk Level:** MEDIUM

#### Files to Create

```
src/WpfMarkdownEditor.Wpf/
├── Theming/
│   ├── EditorTheme.cs              # Theme definition class
│   ├── ITheme.cs                   # Theme interface
│   └── Themes/
│       ├── LightTheme.cs           # Built-in light theme
│       └── DarkTheme.cs            # Built-in dark theme
├── Rendering/
│   ├── FlowDocumentRenderer.cs     # Main renderer
│   ├── IBlockRenderer.cs           # Renderer interface
│   ├── InlineRenderer.cs           # Inline element renderer
│   └── Renderers/
│       ├── HeadingRenderer.cs
│       ├── ParagraphRenderer.cs
│       ├── CodeBlockRenderer.cs
│       ├── TableRenderer.cs
│       ├── BlockquoteRenderer.cs
│       ├── ListRenderer.cs
│       ├── ThematicBreakRenderer.cs
│       └── ImageRenderer.cs
├── SyntaxHighlighting/
│   └── ISyntaxHighlighter.cs       # Interface for Phase 8 integration
└── Converters/
    └── BoolToVisibilityConverter.cs
```

#### Implementation Order

1. **EditorTheme.cs** - Define all theme properties
2. **LightTheme.cs / DarkTheme.cs** - Built-in theme instances
3. **IBlockRenderer.cs** - Interface definition
4. **ISyntaxHighlighter.cs** - Interface definition for Phase 8 integration point
5. **InlineRenderer.cs** - Handle all inline types
6. **Individual Block Renderers** - One per block type
7. **FlowDocumentRenderer.cs** - Orchestration and composition

#### Acceptance Criteria

- [ ] `FlowDocumentRenderer.Render()` produces valid `FlowDocument`
- [ ] All block types render correctly with proper styling
- [ ] Theme colors/fonts applied consistently
- [ ] Light and Dark themes render visibly different output
- [ ] Links are clickable in preview
- [ ] Tables render with correct alignment
- [ ] `ISyntaxHighlighter` interface defined for Phase 8 integration

---

### Phase 5: WPF Controls & Integration
**Duration Estimate:** 2-3 sessions
**Risk Level:** MEDIUM

#### Files to Create

```
src/WpfMarkdownEditor.Wpf/
├── Controls/
│   ├── MarkdownEditor.xaml
│   ├── MarkdownEditor.xaml.cs
│   ├── MarkdownPreview.xaml
│   └── MarkdownPreview.xaml.cs
├── Properties/
│   └── AssemblyInfo.cs
└── Themes/
    └── Generic.xaml               # Default control template
```

#### Control API (per design spec lines 483-597)

**DependencyProperties:**
- `Markdown` (string, two-way binding)
- `Theme` (EditorTheme, one-way)
- `ShowPreview` (bool)
- `PreviewWidth` (GridLength)

**Methods:**
- `LoadFile(string path)`
- `SaveFileAsync(string path)`
- `ApplyTheme(EditorTheme theme)`
- `FocusEditor()`

**Events:**
- `MarkdownChanged`

#### Acceptance Criteria

- [ ] `MarkdownEditor` control renders in Visual Studio designer
- [ ] Two-way binding works for `Markdown` property
- [ ] Preview updates on text change with debounce
- [ ] Theme change applies immediately to preview
- [ ] `ShowPreview` toggle works correctly
- [ ] GridSplitter allows resizing editor/preview panes
- [ ] Control embeds in sample application

---

### Phase 6: Performance & Async Rendering
**Duration Estimate:** 1 session
**Risk Level:** MEDIUM

#### Performance Strategy (per design spec lines 656-731)

```
Pipeline:
User types → TextChanged → Stop debounce timer → Start debounce timer (100ms)
         → On tick: Task.Run(Parse) → Background parse (returns List<Block>)
         → Dispatcher.Invoke(Render) → UI thread creates FlowDocument from blocks
```

**CRITICAL: WPF Threading Model**
- FlowDocument is a DispatcherObject and MUST be created on the UI thread
- Parsing (CPU-bound) runs on background thread via Task.Run()
- Rendering (UI-bound) runs on UI thread via Dispatcher.Invoke()

```csharp
// CORRECT pattern:
var blocks = await Task.Run(() => _parser.Parse(markdown), _cts.Token);
var document = _renderer.Render(blocks); // Must run on UI thread
```

#### Implementation

1. Add `DispatcherTimer` for debouncing
2. Implement `CancellationTokenSource` for cancellation
3. Move parsing to `Task.Run()` - returns `List<Block>`
4. Render FlowDocument on UI thread from parsed blocks
5. Use `Dispatcher.Invoke()` for UI document assignment

#### Performance Targets

| Document Size | Target Time |
|---------------|-------------|
| < 1KB | < 16ms |
| 1KB - 10KB | < 50ms |
| 10KB - 100KB | < 200ms |

#### Acceptance Criteria

- [ ] Debounce prevents excessive re-rendering
- [ ] Typing remains responsive during parse
- [ ] Background thread does not block UI
- [ ] **<50ms update latency for typical documents**

---

### Phase 7: Image Handling
**Duration Estimate:** 1 session
**Risk Level:** LOW

#### Files to Create/Modify

```
src/WpfMarkdownEditor.Wpf/Rendering/Renderers/
├── ImageRenderer.cs               # Block-level images
└── InlineRenderer.cs              # Add inline image support

src/WpfMarkdownEditor.Wpf/Services/
└── ImageLoader.cs                 # Async image loading service
```

#### Image Sources

| Source Type | Strategy |
|-------------|----------|
| Local file (`image.png`) | Load from `BaseDirectory` property |
| Base64 data URI | Decode and display |
| Remote URL (`https://...`) | Async download with placeholder, cache to temp |

#### Acceptance Criteria

- [ ] Local images load from relative paths
- [ ] Base64 embedded images render
- [ ] Remote images load asynchronously
- [ ] Placeholder shown during remote image load
- [ ] Error placeholder shown on failure
- [ ] Temp cache cleaned on application exit

---

### Phase 8: Syntax Highlighting (Optional Module)
**Duration Estimate:** 2 sessions
**Risk Level:** MEDIUM

#### Files to Create

```
src/WpfMarkdownEditor.Wpf/SyntaxHighlighting/
├── SyntaxHighlighter.cs           # Main implementation (ISyntaxHighlighter defined in Phase 4)
├── Lexer.cs                       # Tokenizer base
├── Lexers/
│   ├── CSharpLexer.cs
│   ├── JavaScriptLexer.cs
│   └── PythonLexer.cs
└── Token.cs                       # Syntax token definition
```

**Note:** `ISyntaxHighlighter` interface is defined in Phase 4 as an integration point.

#### Language Support

| Language | Keywords | Comments | Strings | Numbers |
|----------|----------|----------|---------|---------|
| C# | ✓ | ✓ | ✓ | ✓ |
| JavaScript | ✓ | ✓ | ✓ | ✓ |
| Python | ✓ | ✓ | ✓ | ✓ |

#### Acceptance Criteria

- [ ] Code blocks with language hint trigger highlighting
- [ ] Keywords colored correctly for each language
- [ ] Comments identified and styled
- [ ] Strings identified and styled
- [ ] No syntax highlighting degrades to plain code

---

### Phase 9: Sample Application & Final Integration
**Duration Estimate:** 1 session
**Risk Level:** LOW

#### Files to Create

```
samples/
└── WpfMarkdownEditor.Sample/
    ├── App.xaml
    ├── App.xaml.cs
    ├── MainWindow.xaml
    ├── MainWindow.xaml.cs
    └── WpfMarkdownEditor.Sample.csproj
```

#### Sample Features

- Open/Save Markdown files
- Theme switcher (Light/Dark)
- Preview toggle
- Sample Markdown content

#### Acceptance Criteria

- [ ] Sample application launches without errors
- [ ] Can type Markdown and see real-time preview
- [ ] Can switch between Light and Dark themes
- [ ] Can toggle preview visibility
- [ ] Can open and save .md files

---

## Risk Areas

### High Risk

| Risk | Mitigation |
|------|------------|
| **CommonMark 90% Pass Rate** | Start spec tests early (Phase 2); use failure categorization (Must Pass vs Acceptable to Fail); allocate extra time for edge cases |
| **Inline Parsing Complexity** | Implement delimiter stack algorithm per CommonMark spec; reference existing implementations |

### Medium Risk

| Risk | Mitigation |
|------|------------|
| **Performance <50ms** | Profile early; use benchmark tests; implement debouncing; establish baseline in Phase 2 |
| **WPF Threading Issues** | Parse on background thread, render on UI thread (FlowDocument is DispatcherObject); test with large documents |
| **Image Loading** | Handle errors gracefully; implement caching; test various sources |

### Low Risk

| Risk | Mitigation |
|------|------------|
| **NuGet Packaging** | Follow standard .NET library patterns; test local package consumption |
| **Theme System** | Keep theme structure simple; test color contrast |

---

## Test Strategy for CommonMark 90% Compliance

### Test Integration Approach

1. **Download Spec Tests**
   ```bash
   curl -o spec.json https://spec.commonmark.org/0.31/spec.json
   ```

2. **Create Test Adapter**
   ```csharp
   public record CommonMarkTestCase(string markdown, string html, string section, int number);

   public static IEnumerable<CommonMarkTestCase> LoadSpecTests()
   {
       var json = File.ReadAllText("spec.json");
       return JsonSerializer.Deserialize<List<CommonMarkTestCase>>(json);
   }
   ```

3. **Test Runner**
   ```csharp
   [Theory]
   [MemberData(nameof(GetSpecTests))]
   public void CommonMarkSpec(CommonMarkTestCase test)
   {
       var parser = new MarkdownParser();
       var blocks = parser.Parse(test.markdown);
       var html = new HtmlRenderer().Render(blocks);
       Assert.Equal(NormalizeHtml(test.html), NormalizeHtml(html));
   }
   ```

4. **Pass Rate Tracking**
   ```csharp
   [Fact]
   public void CommonMarkPassRate_ShouldBeAtLeast90Percent()
   {
       var total = 0;
       var passed = 0;
       foreach (var test in LoadSpecTests())
       {
           total++;
           try { /* run test */ passed++; }
           catch { /* track failure */ }
       }
       Assert.True((double)passed / total >= 0.90);
   }
   ```

### Coverage Areas

| Category | Spec Sections | Expected Pass Rate |
|----------|---------------|-------------------|
| Tabs | 1 | 100% |
| Preliminaries | 2 | 90% |
| Blocks | 3-6 | 90% |
| Inlines | 7-12 | 90% |
| GFM Tables | Extension | 95% |

### Failure Categorization (CRITICAL for 90% Target)

**MUST PASS (Block 90% target):**
| Feature | Reason |
|---------|--------|
| Emphasis (`*`, `_`) | Core formatting, high user impact |
| Strong (`**`, `__`) | Core formatting, high user impact |
| Links (`[text](url)`) | Core navigation feature |
| Inline code (`` `code` ``) | Essential for technical docs |
| ATX Headings (`# H1`) | Document structure |
| Setext Headings | Alternative heading syntax |
| Fenced code blocks | Essential for code display |
| Paragraphs | Fundamental block type |
| Lists (ordered/unordered) | Common structure |
| Thematic breaks | Document structure |

**ACCEPTABLE TO FAIL (10% margin):**
| Feature | Reason | Risk |
|---------|--------|------|
| HTML block edge cases | Complex HTML interaction | Low - most users use Markdown |
| Complex link references | Full reference link syntax | Low - inline links preferred |
| Setext heading edge cases | Multiple underline lengths | Low - ATX preferred |
| Tab expansion edge cases | Column calculation complexity | Low - spaces common |
| Link reference definitions | Full reference syntax | Low - inline links preferred |

**Test Runner Categorization:**
```csharp
public enum FailureCategory { MustPass, AcceptableToFail }

[Theory]
[MemberData(nameof(GetSpecTests))]
public void CommonMarkSpec(CommonMarkTestCase test)
{
    var parser = new MarkdownParser();
    var blocks = parser.Parse(test.markdown);
    var html = new HtmlRenderer().Render(blocks);
    
    var expected = NormalizeHtml(test.html);
    var actual = NormalizeHtml(html);
    
    if (expected != actual)
    {
        var category = CategorizeFailure(test.section);
        if (category == FailureCategory.MustPass)
        {
            Assert.True(false, $"MUST PASS: {test.section} #{test.number}");
        }
        // AcceptableToFail: log but don't fail
    }
}
```

---

## File Creation Order (Dependency Graph)

```
Phase 1 (Foundation):
  Block.cs, Inline.cs (no deps)
    → Block types (depend on Block, Inline)
    → Inline types (depend on Inline)

Phase 2 (Parser):
  LineReader.cs (no deps)
    → ParserState.cs (no deps)
      → BlockParser.cs (uses LineReader, ParserState, Block types)
        → InlineParser.cs (uses Inline types)
          → MarkdownParser.cs (uses BlockParser, InlineParser)

Phase 3 (Tests):
  Depends on Phase 2

Phase 4 (Rendering):
  EditorTheme.cs (no deps)
    → LightTheme.cs, DarkTheme.cs (use EditorTheme)
      → IBlockRenderer.cs (uses Block)
        → InlineRenderer.cs (uses Inline, EditorTheme)
          → Block Renderers (use IBlockRenderer, Block types, EditorTheme)
            → FlowDocumentRenderer.cs (uses all renderers)

Phase 5 (Controls):
  BoolToVisibilityConverter.cs (no deps)
    → MarkdownPreview.xaml (uses FlowDocumentRenderer)
      → MarkdownEditor.xaml (uses MarkdownPreview, Converter)

Phase 6-8 (Features):
  Depend on Phases 1-5

Phase 9 (Sample):
  Depends on all previous phases
```

---

## Deliverables Summary

| Deliverable | Target Framework | Dependencies |
|-------------|------------------|--------------|
| `WpfMarkdownEditor.Core` | net8.0 | None |
| `WpfMarkdownEditor.Wpf` | net8.0-windows | WpfMarkdownEditor.Core |
| `WpfMarkdownEditor.Core.Tests` | net8.0 | xUnit, Core |
| `WpfMarkdownEditor.Wpf.Tests` | net8.0-windows | xUnit, Core, Wpf |
| `WpfMarkdownEditor.Sample` | net8.0-windows | Wpf |

---

## Open Questions

1. **Remote Image Caching:** Should cached images persist across sessions or be session-only?
   - *Design spec says:* Temp directory, session cleanup
   - *Recommendation:* Session-only, clean on app exit

2. **Syntax Highlighter Scope:** Should we support language auto-detection for code blocks without language hint?
   - *Design spec says:* Only when language specified
   - *Recommendation:* No auto-detection; keep it simple

---

## Next Steps

1. User confirms plan
2. Hand off to `/oh-my-claudecode:start-work wpf-markdown-editor`
3. Begin Phase 1 implementation
