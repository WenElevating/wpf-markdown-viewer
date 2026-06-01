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
    private static readonly DependencyProperty BlockSignatureProperty =
        DependencyProperty.RegisterAttached(
            "BlockSignature",
            typeof(string),
            typeof(FlowDocumentRenderer),
            new PropertyMetadata(null));

    private readonly Dictionary<Type, IBlockRenderer> _renderers;
    private readonly EditorTheme _theme;
    private readonly SolidColorBrush _backgroundBrush;
    private readonly SolidColorBrush _foregroundBrush;

    public FlowDocumentRenderer(EditorTheme theme) : this(theme, null, null) { }

    public FlowDocumentRenderer(EditorTheme theme, IImageResolver? imageResolver) : this(theme, imageResolver, null) { }

    public FlowDocumentRenderer(
        EditorTheme theme,
        IImageResolver? imageResolver,
        SyntaxHighlighter? highlighter = null,
        Action? requestLayoutRefresh = null)
    {
        _theme = theme;
        _backgroundBrush = new SolidColorBrush(theme.BackgroundColor);
        _backgroundBrush.Freeze();
        _foregroundBrush = new SolidColorBrush(theme.ForegroundColor);
        _foregroundBrush.Freeze();
        var cachedImageResolver = imageResolver is null ? null : new CachingImageResolver(imageResolver);
        _renderers = new()
        {
            [typeof(HeadingBlock)] = new HeadingRenderer(theme, cachedImageResolver, requestLayoutRefresh),
            [typeof(ParagraphBlock)] = new ParagraphRenderer(theme, cachedImageResolver, requestLayoutRefresh),
            [typeof(CodeBlock)] = new CodeBlockRenderer(theme, highlighter),
            [typeof(TableBlock)] = new TableRenderer(theme, cachedImageResolver, requestLayoutRefresh),
            [typeof(ListBlock)] = new ListRenderer(theme, cachedImageResolver, requestLayoutRefresh),
            [typeof(ThematicBreakBlock)] = new ThematicBreakRenderer(theme),
            [typeof(ImageBlock)] = new ImageRenderer(theme, cachedImageResolver, requestLayoutRefresh),
        };
        // BlockquoteRenderer references the parent — must be added after dictionary is built
        _renderers[typeof(BlockquoteBlock)] = new BlockquoteRenderer(theme, this);
    }

    /// <summary>
    /// Render blocks to FlowDocument.
    /// </summary>
    public FlowDocument Render(IEnumerable<CoreBlock> blocks)
    {
        var document = CreateDocument();

        foreach (var block in blocks)
        {
            var rendered = RenderBlock(block);
            if (rendered is not null)
                document.Blocks.Add(rendered);
        }

        return document;
    }

    /// <summary>
    /// Updates an existing document in place, reusing unchanged top-level blocks.
    /// </summary>
    public FlowDocument RenderIncremental(FlowDocument? document, IReadOnlyList<CoreBlock> blocks, string markdown)
    {
        document ??= CreateDocument();
        ApplyDocumentStyle(document);

        var existingBlocks = document.Blocks.Cast<System.Windows.Documents.Block>().ToList();
        var reusableBlocks = existingBlocks
            .Select(block => new { Block = block, Signature = GetBlockSignature(block) })
            .Where(item => item.Signature is not null)
            .GroupBy(item => item.Signature!, item => item.Block)
            .ToDictionary(group => group.Key, group => new Queue<System.Windows.Documents.Block>(group), StringComparer.Ordinal);

        var targetBlocks = new List<System.Windows.Documents.Block>(blocks.Count);
        foreach (var block in blocks)
        {
            var signature = CreateBlockSignature(block, markdown);
            if (reusableBlocks.TryGetValue(signature, out var queue) && queue.Count > 0)
            {
                targetBlocks.Add(queue.Dequeue());
                continue;
            }

            var rendered = RenderBlock(block);
            if (rendered is null)
                continue;

            SetBlockSignature(rendered, signature);
            targetBlocks.Add(rendered);
        }

        ApplyBlockOrder(document, targetBlocks);
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

    private FlowDocument CreateDocument()
    {
        var document = new FlowDocument();
        ApplyDocumentStyle(document);
        return document;
    }

    private void ApplyDocumentStyle(FlowDocument document)
    {
        document.Background = _backgroundBrush;
        document.Foreground = _foregroundBrush;
        document.FontFamily = _theme.BodyFont;
        document.FontSize = _theme.BaseFontSize;
        document.PagePadding = _theme.PagePadding;
        document.TextAlignment = TextAlignment.Left;

        if (!double.IsNaN(_theme.LineHeight) && _theme.LineHeight > 0)
            document.LineHeight = _theme.LineHeight;
        else
            document.ClearValue(FlowDocument.LineHeightProperty);
    }

    private static void ApplyBlockOrder(FlowDocument document, IReadOnlyList<System.Windows.Documents.Block> targetBlocks)
    {
        foreach (var block in document.Blocks.Cast<System.Windows.Documents.Block>().ToList())
        {
            if (!targetBlocks.Any(target => ReferenceEquals(target, block)))
                document.Blocks.Remove(block);
        }

        for (var i = 0; i < targetBlocks.Count; i++)
        {
            var currentBlocks = document.Blocks.Cast<System.Windows.Documents.Block>().ToList();
            var target = targetBlocks[i];
            if (i < currentBlocks.Count && ReferenceEquals(currentBlocks[i], target))
                continue;

            if (document.Blocks.Contains(target))
                document.Blocks.Remove(target);

            currentBlocks = document.Blocks.Cast<System.Windows.Documents.Block>().ToList();
            if (i < currentBlocks.Count)
                document.Blocks.InsertBefore(currentBlocks[i], target);
            else
                document.Blocks.Add(target);
        }
    }

    private static string CreateBlockSignature(CoreBlock block, string markdown)
    {
        var source = ExtractSource(markdown, block.LineStart, block.LineEnd);
        return $"{block.GetType().FullName}:{source}";
    }

    private static string ExtractSource(string markdown, int lineStart, int lineEnd)
    {
        if (string.IsNullOrEmpty(markdown) || lineStart <= 0 || lineEnd < lineStart)
            return $"{lineStart}:{lineEnd}";

        var normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var start = Math.Clamp(lineStart - 1, 0, lines.Length - 1);
        var end = Math.Clamp(lineEnd - 1, start, lines.Length - 1);
        return string.Join("\n", lines.Skip(start).Take(end - start + 1));
    }

    private static string? GetBlockSignature(System.Windows.Documents.Block block) =>
        block.GetValue(BlockSignatureProperty) as string;

    private static void SetBlockSignature(System.Windows.Documents.Block block, string signature) =>
        block.SetValue(BlockSignatureProperty, signature);
}
