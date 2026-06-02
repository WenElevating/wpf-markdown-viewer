# HTML Rendering Design

## Summary

Improve Markdown preview rendering for GitHub-style README files that mix Markdown with safe, common HTML. The implementation adds dedicated HTML parsing support in Core and a dedicated WPF `HtmlRenderer` so HTML handling is isolated from normal paragraph rendering.

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
- Avoid new package dependencies.

## Non-goals

- Do not execute scripts or support interactive/browser HTML.
- Do not support `iframe`, `script`, `style`, embedded forms, arbitrary CSS layout, or remote HTML execution.
- Do not implement collapsible `details` interactivity in the first version.
- Do not try to match a browser pixel-for-pixel.
- Do not introduce an external HTML parser package.
- Do not change the behavior of normal Markdown blocks except where mixed HTML currently renders incorrectly.

## Architecture

### Core HTML Model

Add framework-agnostic HTML node types under `WpfMarkdownEditor.Core`:

- `HtmlBlock : Block`
- `HtmlInline : Inline`
- `HtmlElementNode`
- `HtmlTextNode`

The model stores tag name, attributes, children, source line range, and text content. It should preserve enough structure for renderers without storing WPF concepts.

### Core HTML Parser

Add a small zero-dependency parser for the supported subset. It should:

- Parse opening tags, closing tags, self-closing tags, quoted attributes, bare attributes, and text.
- Decode common HTML entities in text and attribute values.
- Treat unsupported tags as transparent containers when possible, preserving their text children.
- Never expose executable behavior.

`BlockParser` should detect block-level HTML at paragraph boundaries and produce `HtmlBlock` when a recognized block tag begins a block. `InlineParser` should delegate recognized inline HTML to the same HTML parsing helper and produce `HtmlInline` or existing image/link inline nodes where that is simpler and compatible.

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

### Relative Image Base Directory

Add document source context to the editor/rendering path:

- `MarkdownEditor` gets a `DocumentPath` or `DocumentBaseDirectory` property.
- `LoadFile(path)` sets the base directory before rendering.
- New/untitled documents clear the base directory.
- `ImageLoader` can update or be recreated with the current base directory.
- Converters that receive `DocumentConversionRequest.FilePath` should pass that file directory to rendering when possible.

If no base directory is available, existing behavior is preserved.

## Data Flow

1. The editor loads Markdown text and, when available, records the current file path.
2. `MarkdownParser` parses Markdown into block AST nodes, including `HtmlBlock` for supported block HTML.
3. `FlowDocumentRenderer` dispatches normal Markdown blocks to existing renderers and HTML blocks to `HtmlRenderer`.
4. `HtmlRenderer` renders safe HTML nodes into WPF document blocks and inlines.
5. Image nodes resolve through `ImageLoader`, which uses the current document base directory for relative paths.

## Error Handling and Degradation

- Malformed supported HTML should render its recoverable text children.
- Unknown tags should not show raw markup unless there is no meaningful text content to show.
- Missing images should use the existing broken-image rendering behavior.
- Invalid image widths should be ignored.
- Invalid or unsafe URLs should render as text instead of clickable links.

## Testing Plan

Use test-first implementation.

Core parser tests:

- Parses a centered `<div><p><img ... /></p></div>` into an `HtmlBlock`.
- Parses `<br>` as line break content instead of visible raw text.
- Parses `<details><summary><b>...</b></summary>...</details>` preserving summary and children.
- Parses simple HTML table rows and cells.
- Falls back safely for malformed or unsupported tags.

WPF renderer tests:

- Renders centered header/logo HTML without raw tag text.
- Renders `<summary>` as bold visible text and includes details body text.
- Renders HTML table as a WPF table-like block.
- Renders HTML images through the existing image element path.
- Resolves relative image paths from the opened Markdown file directory.

Sample regression fixture:

- Use representative snippets from `D:\Test\cursor-free-vip-1.11.03\README.md` covering logo/header, feature list `<br>`, details sections, contributor image link, and donation image table.

Verification:

- Run focused Core and WPF test projects.
- Run `dotnet test WpfMarkdownEditor.sln -v minimal`.
- Manually open the sample app with `D:\Test\cursor-free-vip-1.11.03\README.md` and inspect the preview.
