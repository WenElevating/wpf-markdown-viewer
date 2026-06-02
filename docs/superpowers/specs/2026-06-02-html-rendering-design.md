# HTML Rendering Design

## Summary

Improve Markdown preview rendering for GitHub-style README files that mix Markdown with safe, common HTML. The implementation adds dedicated HTML handling in Core and a dedicated WPF `HtmlRenderer` so HTML rendering stays isolated from normal paragraph rendering.

The first version targets the common README HTML subset found in `D:\Test\cursor-free-vip-1.11.03\README.md`: centered logo/header blocks, inline line breaks, linked images, default-open details sections, and simple image tables.

## Current State

- `BlockParser` treats most HTML as normal paragraph text.
- `InlineParser` only recognizes raw `<img>` and simple `<a><img></a>` patterns and converts them to `ImageInline`.
- `FlowDocumentRenderer` dispatches by Markdown block type and has no HTML-specific block renderer.
- `ParagraphRenderer` contains image-specific special cases but should not become responsible for parsing or rendering arbitrary HTML.
- `MarkdownEditor` creates `ImageLoader` with `AppContext.BaseDirectory`, so relative images are not resolved from the opened Markdown file's directory.

This explains the poor preview for READMEs containing `<div align="center">`, `<p>`, `<br>`, `<details>`, `<summary>`, and HTML tables.

## Goals

- Add a dedicated HTML rendering path instead of expanding paragraph rendering.
- Support a safe GitHub README HTML subset:
  - block/container tags: `div`, `p`, `center`, `details`, `summary`
  - headings: `h1`, `h2`, `h3`, `h4`, `h5`, `h6`
  - inline formatting: `b`, `strong`, `i`, `em`, `code`, `br`, `a`, `img`
  - simple tables: `table`, `thead`, `tbody`, `tr`, `td`, `th`
- Render `<details>` as expanded content, with `<summary>` as a bold summary heading.
- Respect common presentation attributes where useful and safe:
  - `align="center"` maps to centered WPF text/content alignment.
  - `width` constrains rendered image width.
  - `src`, `alt`, `title`, and `href` are used for image/link rendering.
- Resolve relative image paths from the Markdown file's directory when the editor opened a file.
- Keep Core independent from WPF APIs.
- Do not add external package dependencies.
- Implement the HTML parser as a handwritten supported-subset parser based on `docs/html-subset-parser.md`.

## Non-goals

- Do not execute scripts or support interactive/browser HTML.
- Do not support `iframe`, `script`, `style`, embedded forms, arbitrary CSS layout, or remote HTML execution.
- Do not implement collapsible `details` interactivity in the first version.
- Do not try to match a browser pixel-for-pixel.
- Do not introduce `HtmlAgilityPack` or another external HTML parser package.
- Do not use `System.Xml.Linq.XDocument` as the ingestion path for README HTML fragments.
- Do not change the behavior of normal Markdown blocks except where mixed HTML currently renders incorrectly.

## Architecture

### HTML Parser Decision

HTML parsing has real edge cases: quoted and unquoted attributes, mixed casing, void tags, nested same-name elements, comments, document declarations, and malformed markup. The implementation should not pretend a tiny parser is a general HTML parser.

The parser decision is fixed for this feature: do not add external dependencies. Implement a handwritten HTML subset parser using the two-layer design in `docs/html-subset-parser.md`:

```text
raw string
   -> HtmlTokenizer: character scanner that produces tokens
   -> HtmlSubsetParser: token stream to HtmlElementNode tree
```

This is acceptable only because the supported input is a GitHub README subset, not arbitrary web HTML. The parser must be documented and tested as a subset parser, not a general HTML parser.

`System.Xml.Linq.XDocument` is excluded because README HTML is often not well-formed XML. Examples include `<br>`, `<img ...>`, mixed fragments without a single root, and bare attributes.

### Core HTML Model

Add framework-agnostic HTML node types under `WpfMarkdownEditor.Core`:

- `HtmlBlock : Block`
- `HtmlInline : Inline`
- `HtmlElementNode`
- `HtmlTextNode`

The model stores tag name, attributes, children, source line range, text content, and a small amount of semantic metadata. It should preserve enough structure for renderers without storing WPF concepts.

`HtmlElementNode` should include:

- `TagName`: normalized lowercase tag name.
- `Attributes`: case-insensitive dictionary of decoded attribute values.
- `Children`: child nodes.
- `SourceStart` and `SourceEnd`: offsets or line range for diagnostics and incremental signatures.
- `IsVoidElement`: true for tags such as `br` and `img`.

`HtmlBlock` should include the parsed root nodes and a `SourceKind` such as `BlockFragment`. `HtmlInline` should include parsed inline nodes and a `SourceKind` such as `InlineFragment`.

### Core HTML Parser

Add a parser facade with two explicit entry points:

- `TryParseBlockFragment(string source, int startIndex, out HtmlFragment fragment, out int consumedLineCount)`
- `TryParseInlineFragment(string source, int startIndex, out HtmlFragment fragment, out int consumedLength)`

The facade must use the handwritten tokenizer/parser internally. `BlockParser` and `InlineParser` depend only on the facade. Both entry points accept `startIndex` for symmetry; `BlockParser` normally passes the start offset of the first non-space character in the current line, while `InlineParser` passes the current inline scan offset.

Tokenizer design:

- `HtmlTokenKind.Text`
- `HtmlTokenKind.OpenTag`
- `HtmlTokenKind.CloseTag`
- `HtmlTokenKind.SelfClose`

`HtmlToken` carries `Kind`, normalized lowercase `Name`, decoded case-insensitive attributes, and optional decoded text.

Tokenizer behavior:

- Character-level scanner, no regex-driven HTML parsing.
- `ReadName` accepts `[a-zA-Z0-9_:-]` and stops on any other character.
- Attribute values support double quotes, single quotes, and unquoted values.
- Attribute keys use a case-insensitive dictionary.
- `br`, `img`, `hr`, and `input` are hard-coded void elements and produce `SelfClose`.
- `<br>`, `<br/>`, and `<br />` are equivalent.
- Malformed tags skip to the next `>` and degrade instead of throwing.
- `<!-- ... -->` comments are skipped.
- `<!doctype ...>` and declarations are skipped.
- Entity decoding supports at least `&amp;`, `&lt;`, `&gt;`, `&quot;`, `&apos;`, `&nbsp;`, and `&#39;`; numeric entities may be added if needed by tests.

Parser design:

- Parse tokens with a stack rooted at an internal `__root__` node.
- Supported tags are exactly the approved subset unless this spec is updated.
- Supported open tags are added as `HtmlElementNode` children and pushed onto the stack.
- Supported self-closing tags are added as leaf `HtmlElementNode` children.
- Unsupported open tags are not pushed; their recoverable text falls through to the current parent.
- Closing tags pop only when they match the current stack top; mismatched closing tags are ignored.
- Unclosed tags left on the stack at EOF are treated as completed.
- Tag names are normalized with `ToLowerInvariant()`.
- Opening a new `<p>` while the stack top is `<p>` implicitly closes the previous paragraph before pushing the new one.
- `<details>` and `<summary>` receive no parser special case; the renderer finds the first summary child.

Known parser limits:

- This parser does not support arbitrary HTML, CSS, scripts, forms, or browser layout.
- `<script>` and `<style>` are outside the supported subset and should not render executable or styled content.
- `<pre>` whitespace preservation is not supported in the first version.
- Attribute values containing `>` are not a supported edge case unless covered by tokenizer tests.

`BlockParser` owns block-vs-inline classification:

- At the start of a block, if the first non-space token is an approved block tag (`div`, `p`, `center`, `details`, `summary`, `table`, `h1` to `h6`), call `TryParseBlockFragment`.
- A block fragment consumes lines until its tag stack returns to the root after at least one block tag was parsed, or EOF is reached.
- Blank lines inside an open HTML tag stack are part of the fragment and are tokenized as text/spacing. They must not terminate the fragment.
- A blank line only terminates HTML block parsing when the parser is at root stack depth and the current fragment is already balanced.
- A block-level tag in the middle of an already-started Markdown paragraph is not promoted to `HtmlBlock`; it is treated as inline/fallback content by `InlineParser`.
- Mixed lines such as `Some text <div align="center">logo</div> more text` deliberately degrade the block-level tag to text. This is not an error condition; block-level HTML is supported only when it starts a block.

`InlineParser` owns only inline HTML inside Markdown text:

- If it sees `<br>`, `<b>`, `<strong>`, `<i>`, `<em>`, `<code>`, `<a>`, or `<img>` at the current offset, call `TryParseInlineFragment`.
- Inline parsing stops at the first balanced inline fragment.
- Inline parsing must not consume block-level tags from the middle of text.

### WPF HtmlRenderer

Add `WpfMarkdownEditor.Wpf.Rendering.Renderers.HtmlRenderer`. `FlowDocumentRenderer` registers it for `HtmlBlock`.

`HtmlRenderer` owns all HTML-to-FlowDocument mapping:

- `div`, `center`, `p`: render child content in sections or paragraphs, carrying alignment.
- `h1` to `h6`: render with heading-like font sizes and margins.
- `details`: render all children expanded.
- `summary`: render as a bold paragraph or heading-like paragraph.
- `table`, `tr`, `td`, `th`: render as WPF `Table`, matching existing table styling where possible.
- `a`: render child text/images as a link where WPF supports it; image links can render the image and ignore click behavior if wrapping an image is not practical.
- `img`: use the existing image element factory and image resolver path.
- `br`: render as a WPF `LineBreak`.
- `b`, `strong`, `i`, `em`, `code`: map to existing inline styling patterns.

Unknown nodes fall back to rendering visible text content without raw tag markup.

### Existing Raw Image Logic

The current `InlineParser` special cases for raw `<img>` and `<a><img></a>` should be retired or made a thin compatibility wrapper over `TryParseInlineFragment`.

Priority rules:

1. `InlineParser` calls the HTML parser facade for supported inline HTML at `<`.
2. If the parsed inline fragment is a single `<img>`, it maps to the existing `ImageInline` path where possible.
3. If the parsed inline fragment is a single `<a>` containing one `<img>`, it maps to an `HtmlInline` so the renderer can preserve link/image semantics without losing structure.
4. If HTML parsing fails, the existing Markdown fallback treats `<` as text.

There should not be two independent raw HTML image parsers after this change.

### Details Future Extension

The first version renders `<details>` expanded because `FlowDocument` is best suited to static document content. The Core model should still preserve `details`, `summary`, and the `open` attribute rather than flattening them during parsing.

This leaves a future path open:

- Static preview can continue to use `HtmlRenderer`.
- An interactive preview could render `details` as `BlockUIContainer` with an `Expander`.
- The AST does not need to change for that future renderer because the semantic tags are preserved.

### Relative Image Base Directory

Add document source context to the editor/rendering path:

- `MarkdownEditor` gets a `DocumentPath` dependency property and derives `DocumentBaseDirectory` from it.
- `LoadFile(path)` sets `DocumentPath` before setting `Markdown`, so the first render uses the file directory.
- New/untitled documents clear `DocumentPath`.
- Save As updates `DocumentPath` to the selected path before marking the document clean and triggers a preview rerender, because relative image resolution may change.
- Move updates `DocumentPath` to the moved path and triggers a preview rerender.
- `ImageLoader` can update or be recreated with the current base directory when `DocumentPath` changes.
- Converters that receive `DocumentConversionRequest.FilePath` should pass that file directory to rendering when possible.

If no base directory is available, existing behavior is preserved.

## Data Flow

1. The editor loads Markdown text and, when available, records the current file path.
2. `MarkdownParser` parses Markdown into block AST nodes, including `HtmlBlock` for supported block HTML.
3. `FlowDocumentRenderer` dispatches normal Markdown blocks to existing renderers and HTML blocks to `HtmlRenderer`.
4. `HtmlRenderer` renders safe HTML nodes into WPF document blocks and inlines.
5. Image nodes resolve through `ImageLoader`, which uses the current document base directory for relative paths.
6. If the current document path changes through Save As or Move, the editor updates image resolution context and rerenders the preview.

## Error Handling and Degradation

- Malformed supported HTML should render its recoverable text children.
- Unknown tags should not show raw markup unless there is no meaningful text content to show.
- Missing images should use the existing broken-image rendering behavior.
- Invalid image widths should be ignored.
- Invalid or unsafe URLs should render as text instead of clickable links.

## Testing Plan

Use test-first implementation.

Tokenizer/parser tests:

- Tokenizes text, open tags, close tags, and self-closing tags.
- Tokenizes `<br>`, `<br/>`, and `<br />` as self-closing `br`.
- Tokenizes `<IMG SRC=...>`, mixed-case tag names, single-quoted attributes, double-quoted attributes, and unquoted attributes.
- Skips comments and declarations.
- Decodes supported HTML entities in text and attributes.
- Builds nested same-name tags using stack behavior.
- Handles implicit `<p>` close when a new `<p>` opens inside a current `<p>`.
- Ignores mismatched closing tags without throwing.
- Add a test proving malformed unsupported HTML degrades to text rather than throwing.

Core parser tests:

- Parses a centered `<div><p><img ... /></p></div>` into an `HtmlBlock`.
- Parses `<br>` as line break content instead of visible raw text.
- Parses `<details><summary><b>...</b></summary>...</details>` preserving summary and children.
- Parses simple HTML table rows and cells.
- Falls back safely for malformed or unsupported tags.
- Does not promote a block-level `<div>` in the middle of a Markdown paragraph to `HtmlBlock`.
- Uses one HTML parser path for raw `<img>` instead of the legacy independent parser.

WPF renderer tests:

- Renders centered header/logo HTML without raw tag text.
- Renders `<summary>` as bold visible text and includes details body text.
- Renders HTML table as a WPF table-like block.
- Renders HTML images through the existing image element path.
- Resolves relative image paths from the opened Markdown file directory.
- Re-renders relative images from the new directory after Save As changes `DocumentPath`.

WPF test execution requirements:

- Tests that instantiate `FlowDocument`, `Paragraph`, `Table`, image containers, or `MarkdownEditor` must run on an STA thread with a WPF `Dispatcher`.
- The WPF test project should use an explicit STA helper or test attribute rather than relying on the default xUnit worker thread.
- Initialize `Application.Current` only when a test requires application resources; renderer-level structural tests should avoid unnecessary app startup.
- If a renderer behavior can be verified before WPF object creation, prefer Core AST or renderer-helper tests to reduce Dispatcher coupling.

Sample regression fixture and automated coverage:

- Use representative snippets from `D:\Test\cursor-free-vip-1.11.03\README.md` covering logo/header, feature list `<br>`, details sections, contributor image link, and donation image table.
- Add deterministic structural tests over the rendered `FlowDocument` for those snippets: no raw HTML tag text, expected block count shape, expected summary text, expected table cell count, and expected image loading containers.
- Add optional screenshot or XAML snapshot coverage only if it is deterministic in this test environment. Prefer structural assertions for CI stability.

Verification:

- Run focused Core and WPF test projects.
- Run `dotnet test WpfMarkdownEditor.sln -v minimal`.
- Manually open the sample app with `D:\Test\cursor-free-vip-1.11.03\README.md` and inspect the preview as a final human check, not as the only regression guard.
