using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Wpf.Rendering.Renderers;
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

    public FlowDocumentRenderer(EditorTheme theme)
    {
        _theme = theme;
        _renderers = new()
        {
            [typeof(HeadingBlock)] = new HeadingRenderer(theme),
            [typeof(ParagraphBlock)] = new ParagraphRenderer(theme),
            [typeof(CodeBlock)] = new CodeBlockRenderer(theme),
            [typeof(TableBlock)] = new TableRenderer(theme),
            [typeof(BlockquoteBlock)] = new BlockquoteRenderer(theme),
            [typeof(ListBlock)] = new ListRenderer(theme),
            [typeof(ThematicBreakBlock)] = new ThematicBreakRenderer(theme),
            [typeof(ImageBlock)] = new ImageRenderer(theme),
        };
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
            PagePadding = new Thickness(16),
        };

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
