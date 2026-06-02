using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using WpfMarkdownEditor.Core;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Html;
using WpfMarkdownEditor.Core.Parsing.Inlines;
using WpfMarkdownEditor.Wpf.Theming;
using CoreBlock = WpfMarkdownEditor.Core.Parsing.Block;
using WpfBlock = System.Windows.Documents.Block;

namespace WpfMarkdownEditor.Wpf.Rendering.Renderers;

public sealed class HtmlRenderer(
    EditorTheme theme,
    IImageResolver? imageResolver = null,
    Action? requestLayoutRefresh = null) : IBlockRenderer
{
    public WpfBlock Render(CoreBlock block)
    {
        var html = (HtmlBlock)block;
        var section = new Section { Margin = new Thickness(0) };
        RenderBlockNodes(section.Blocks, html.Fragment.Children, TextAlignment.Left);
        return section;
    }

    public void RenderInlineNodes(InlineCollection target, IReadOnlyList<HtmlNode> nodes)
    {
        foreach (var node in nodes)
            RenderInlineNode(target, node);
    }

    private void RenderBlockNodes(BlockCollection blocks, IReadOnlyList<HtmlNode> nodes, TextAlignment alignment)
    {
        var inlineBuffer = new List<HtmlNode>();

        foreach (var node in nodes)
        {
            if (node is HtmlElementNode element && IsBlockElement(element.TagName))
            {
                FlushInlineBuffer(blocks, inlineBuffer, alignment);
                RenderBlockElement(blocks, element, alignment);
                continue;
            }

            inlineBuffer.Add(node);
        }

        FlushInlineBuffer(blocks, inlineBuffer, alignment);
    }

    private void RenderBlockElement(BlockCollection blocks, HtmlElementNode element, TextAlignment inheritedAlignment)
    {
        var alignment = GetAlignment(element, inheritedAlignment);

        switch (element.TagName)
        {
            case "div":
            case "center":
            case "details":
                var section = new Section
                {
                    Margin = new Thickness(0),
                    TextAlignment = alignment,
                };
                RenderBlockNodes(section.Blocks, element.Children, alignment);
                if (section.Blocks.Count > 0)
                    blocks.Add(section);
                break;

            case "table":
                blocks.Add(CreateTable(element));
                break;

            case "p":
                blocks.Add(CreateParagraph(element.Children, alignment));
                break;

            case "summary":
                blocks.Add(CreateSummary(element.Children, alignment));
                break;

            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                blocks.Add(CreateHeading(element, alignment));
                break;

            default:
                FlushInlineBuffer(blocks, [element], alignment);
                break;
        }
    }

    private void FlushInlineBuffer(BlockCollection blocks, List<HtmlNode> inlineBuffer, TextAlignment alignment)
    {
        if (inlineBuffer.Count == 0)
            return;

        if (!inlineBuffer.Any(HasVisibleContent))
        {
            inlineBuffer.Clear();
            return;
        }

        blocks.Add(CreateParagraph(inlineBuffer, alignment));
        inlineBuffer.Clear();
    }

    private Paragraph CreateParagraph(IReadOnlyList<HtmlNode> nodes, TextAlignment alignment)
    {
        var paragraph = new Paragraph
        {
            FontFamily = theme.BodyFont,
            Foreground = new SolidColorBrush(theme.ForegroundColor),
            TextAlignment = alignment,
            Margin = new Thickness(0, 0, 0, theme.ParagraphSpacing),
        };
        RenderInlineNodes(paragraph.Inlines, nodes);
        return paragraph;
    }

    private Paragraph CreateSummary(IReadOnlyList<HtmlNode> nodes, TextAlignment alignment)
    {
        var paragraph = CreateParagraph(nodes, alignment);
        paragraph.FontWeight = FontWeights.Bold;
        return paragraph;
    }

    private Paragraph CreateHeading(HtmlElementNode element, TextAlignment alignment)
    {
        var level = element.TagName.Length == 2 && char.IsDigit(element.TagName[1])
            ? element.TagName[1] - '0'
            : 6;

        var paragraph = new Paragraph
        {
            FontFamily = theme.HeadingFont,
            Foreground = new SolidColorBrush(theme.HeadingColor),
            FontSize = GetHeadingFontSize(level),
            FontWeight = FontWeights.Bold,
            TextAlignment = alignment,
            Margin = new Thickness(0, theme.HeadingMarginTop, 0, theme.HeadingMarginBottom),
        };
        RenderInlineNodes(paragraph.Inlines, element.Children);
        return paragraph;
    }

    private Table CreateTable(HtmlElementNode tableNode)
    {
        var rows = Descendants(tableNode)
            .OfType<HtmlElementNode>()
            .Where(static node => node.TagName == "tr")
            .Select(static row => new
            {
                Row = row,
                Cells = row.Children
                    .OfType<HtmlElementNode>()
                    .Where(static cell => cell.TagName is "td" or "th")
                    .ToList()
            })
            .Where(static item => item.Cells.Count > 0)
            .ToList();

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 8, 0, theme.ParagraphSpacing),
            BorderBrush = new SolidColorBrush(theme.TableBorderColor),
            BorderThickness = new Thickness(1),
        };

        var columnCount = rows.Count == 0 ? 0 : rows.Max(static row => row.Cells.Count);
        for (var i = 0; i < columnCount; i++)
            table.Columns.Add(new TableColumn());

        var rowGroup = new TableRowGroup();
        table.RowGroups.Add(rowGroup);

        foreach (var rowNode in rows)
        {
            var row = new TableRow();
            foreach (var cellNode in rowNode.Cells)
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(0),
                    Padding = new Thickness(6),
                    TextAlignment = GetAlignment(cellNode, TextAlignment.Left),
                };
                if (cellNode.TagName == "th")
                    paragraph.FontWeight = FontWeights.Bold;

                RenderInlineNodes(paragraph.Inlines, cellNode.Children);

                row.Cells.Add(new TableCell(paragraph)
                {
                    BorderBrush = new SolidColorBrush(theme.TableBorderColor),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                });
            }

            rowGroup.Rows.Add(row);
        }

        return table;
    }

    private void RenderInlineNode(InlineCollection target, HtmlNode node)
    {
        switch (node)
        {
            case HtmlTextNode text:
                target.Add(new Run(text.Text));
                break;

            case HtmlElementNode { TagName: "br" }:
                target.Add(new LineBreak());
                break;

            case HtmlElementNode { TagName: "b" or "strong" } element:
                target.Add(CreateBold(element.Children));
                break;

            case HtmlElementNode { TagName: "i" or "em" } element:
                target.Add(CreateItalic(element.Children));
                break;

            case HtmlElementNode { TagName: "code" } element:
                target.Add(CreateCode(element.Children));
                break;

            case HtmlElementNode { TagName: "a" } element:
                target.Add(CreateHyperlink(element));
                break;

            case HtmlElementNode { TagName: "img" } element:
                target.Add(InlineImageRenderer.Render(CreateImageInline(element), theme, imageResolver, requestLayoutRefresh));
                break;

            case HtmlElementNode element:
                target.Add(new Run(RenderTextFallback(element)));
                break;
        }
    }

    private Bold CreateBold(IReadOnlyList<HtmlNode> children)
    {
        var span = new Span();
        RenderInlineNodes(span.Inlines, children);
        return new Bold(span);
    }

    private Italic CreateItalic(IReadOnlyList<HtmlNode> children)
    {
        var span = new Span();
        RenderInlineNodes(span.Inlines, children);
        return new Italic(span);
    }

    private Span CreateCode(IReadOnlyList<HtmlNode> children)
    {
        var span = new Span
        {
            FontFamily = theme.CodeFont,
            FontSize = 13,
            Background = new SolidColorBrush(theme.InlineCodeBackground),
            Foreground = new SolidColorBrush(theme.InlineCodeForeground),
        };
        RenderInlineNodes(span.Inlines, children);
        return span;
    }

    private Hyperlink CreateHyperlink(HtmlElementNode element)
    {
        var span = new Span();
        RenderInlineNodes(span.Inlines, element.Children);

        var hyperlink = new Hyperlink(span)
        {
            Foreground = new SolidColorBrush(theme.LinkColor),
            TextDecorations = null,
        };

        if (element.Attributes.TryGetValue("href", out var href) &&
            Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            hyperlink.NavigateUri = uri;
            hyperlink.RequestNavigate += (s, e) =>
            {
                Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
                e.Handled = true;
            };
        }

        return hyperlink;
    }

    private static ImageInline CreateImageInline(HtmlElementNode element) =>
        new()
        {
            Url = element.Attributes.TryGetValue("src", out var src) ? src : string.Empty,
            Alt = element.Attributes.TryGetValue("alt", out var alt) ? alt : null,
            Title = element.Attributes.TryGetValue("title", out var title) ? title : null
        };

    private static string RenderTextFallback(HtmlElementNode element) =>
        string.Concat(element.Children.Select(RenderTextFallback));

    private static string RenderTextFallback(HtmlNode node) =>
        node switch
        {
            HtmlTextNode text => text.Text,
            HtmlElementNode element => RenderTextFallback(element),
            _ => string.Empty
        };

    private static TextAlignment GetAlignment(HtmlElementNode element, TextAlignment fallback)
    {
        if (string.Equals(element.TagName, "center", StringComparison.OrdinalIgnoreCase))
            return TextAlignment.Center;

        return element.Attributes.TryGetValue("align", out var value) &&
               string.Equals(value, "center", StringComparison.OrdinalIgnoreCase)
            ? TextAlignment.Center
            : fallback;
    }

    private double GetHeadingFontSize(int level) => level switch
    {
        1 => Math.Round(theme.BaseFontSize * 1.85),
        2 => Math.Round(theme.BaseFontSize * 1.5),
        3 => Math.Round(theme.BaseFontSize * 1.25),
        4 => Math.Round(theme.BaseFontSize * 1.1),
        5 => theme.BaseFontSize,
        6 => theme.BaseFontSize,
        _ => theme.BaseFontSize,
    };

    private static bool IsBlockElement(string tagName) =>
        tagName is "div" or "center" or "details" or "p" or "summary" or "table" or
            "h1" or "h2" or "h3" or "h4" or "h5" or "h6";

    private static IEnumerable<HtmlNode> Descendants(HtmlElementNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            if (child is HtmlElementNode element)
            {
                foreach (var descendant in Descendants(element))
                    yield return descendant;
            }
        }
    }

    private static bool HasVisibleContent(HtmlNode node) =>
        node switch
        {
            HtmlTextNode text => !string.IsNullOrWhiteSpace(text.Text),
            HtmlElementNode { TagName: "br" or "img" } => true,
            HtmlElementNode element => element.Children.Any(HasVisibleContent),
            _ => false
        };
}
