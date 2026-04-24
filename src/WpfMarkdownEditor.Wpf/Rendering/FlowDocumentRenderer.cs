using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Rendering.Renderers;
using WpfMarkdownEditor.Wpf.SyntaxHighlighting;
using WpfMarkdownEditor.Wpf.Theming;
using CoreBlock = WpfMarkdownEditor.Core.Parsing.Block;

namespace WpfMarkdownEditor.Wpf.Rendering;

/// <summary>
/// Renders Block AST to WPF FlowDocument.
/// </summary>
public sealed class FlowDocumentRenderer
{
    private readonly Dictionary<Type, IBlockRenderer> _renderers;
    private readonly EditorTheme _theme;

    public FlowDocumentRenderer(EditorTheme theme) : this(theme, null, null) { }

    public FlowDocumentRenderer(EditorTheme theme, IImageResolver? imageResolver) : this(theme, imageResolver, null) { }

    public FlowDocumentRenderer(EditorTheme theme, IImageResolver? imageResolver, SyntaxHighlighter? highlighter)
    {
        _theme = theme;
        _renderers = new()
        {
            [typeof(HeadingBlock)] = new HeadingRenderer(theme, imageResolver),
            [typeof(ParagraphBlock)] = new ParagraphRenderer(theme, imageResolver),
            [typeof(CodeBlock)] = new CodeBlockRenderer(theme, highlighter),
            [typeof(TableBlock)] = new TableRenderer(theme),
            [typeof(ListBlock)] = new ListRenderer(theme, imageResolver),
            [typeof(ThematicBreakBlock)] = new ThematicBreakRenderer(theme),
            [typeof(ImageBlock)] = new ImageRenderer(theme, imageResolver),
        };
        // BlockquoteRenderer references the parent — must be added after dictionary is built
        _renderers[typeof(BlockquoteBlock)] = new BlockquoteRenderer(theme, this);
    }

    /// <summary>
    /// Render blocks to FlowDocument.
    /// </summary>
    public FlowDocument Render(IEnumerable<CoreBlock> blocks)
    {
        var document = new FlowDocument
        {
            Background = new SolidColorBrush(_theme.BackgroundColor),
            Foreground = new SolidColorBrush(_theme.ForegroundColor),
            FontFamily = _theme.BodyFont,
            FontSize = _theme.BaseFontSize,
            PagePadding = _theme.PagePadding,
        };

        if (!double.IsNaN(_theme.LineHeight) && _theme.LineHeight > 0)
            document.LineHeight = _theme.LineHeight;

        foreach (var block in blocks)
        {
            var rendered = RenderBlock(block);
            if (rendered is not null)
                document.Blocks.Add(rendered);
        }

        return document;
    }

    /// <summary>
    /// Render a single block. Public for nested rendering (e.g., blockquotes).
    /// </summary>
    public System.Windows.Documents.Block? RenderBlock(CoreBlock block)
    {
        if (_renderers.TryGetValue(block.GetType(), out var renderer))
            return renderer.Render(block);
        return null;
    }
}
