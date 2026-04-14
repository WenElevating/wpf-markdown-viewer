using System.Text;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Inlines;

namespace WpfMarkdownEditor.Core.Tests.Parsing;

/// <summary>
/// Renders Block AST to HTML for CommonMark spec test validation.
/// Test-only utility — lives in the test project.
/// </summary>
internal sealed class HtmlRenderer
{
    public string Render(List<Block> blocks)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < blocks.Count; i++)
        {
            RenderBlock(blocks[i], sb);
            if (i < blocks.Count - 1)
                sb.AppendLine();
        }
        return sb.ToString();
    }

    private void RenderBlock(Block block, StringBuilder sb)
    {
        switch (block)
        {
            case HeadingBlock h:
                sb.Append($"<h{h.Level}>");
                RenderInlines(h.Inlines, sb);
                sb.Append($"</h{h.Level}>");
                break;

            case ParagraphBlock p:
                sb.Append("<p>");
                RenderInlines(p.Inlines, sb);
                sb.Append("</p>");
                break;

            case CodeBlock c:
                sb.Append("<pre><code");
                if (c.Language is not null)
                    sb.Append($" class=\"language-{c.Language}\"");
                sb.Append('>');
                sb.Append(EscapeHtml(c.Code));
                sb.Append("</code></pre>");
                break;

            case BlockquoteBlock bq:
                sb.Append("<blockquote>");
                foreach (var child in bq.Children)
                {
                    sb.AppendLine();
                    RenderBlock(child, sb);
                }
                sb.AppendLine();
                sb.Append("</blockquote>");
                break;

            case ListBlock list:
                var tag = list.IsOrdered ? "ol" : "ul";
                sb.Append($"<{tag}>");
                sb.AppendLine();
                foreach (var item in list.Items)
                {
                    sb.Append("<li>");
                    foreach (var itemBlock in item.Blocks)
                        RenderBlock(itemBlock, sb);
                    sb.Append("</li>");
                    sb.AppendLine();
                }
                sb.Append($"</{tag}>");
                break;

            case ThematicBreakBlock:
                sb.Append("<hr />");
                break;

            case TableBlock table:
                sb.Append("<table>");
                sb.AppendLine("<thead>");
                sb.Append("<tr>");
                for (var i = 0; i < table.Headers.Count; i++)
                {
                    var align = i < table.Alignments.Count ? table.Alignments[i] : TableBlock.TableAlignment.Left;
                    var alignAttr = align switch
                    {
                        TableBlock.TableAlignment.Center => " align=\"center\"",
                        TableBlock.TableAlignment.Right => " align=\"right\"",
                        _ => ""
                    };
                    sb.Append($"<th{alignAttr}>{EscapeHtml(table.Headers[i])}</th>");
                }
                sb.Append("</tr>");
                sb.AppendLine("</thead>");
                sb.AppendLine("<tbody>");
                foreach (var row in table.Rows)
                {
                    sb.Append("<tr>");
                    for (var i = 0; i < row.Count; i++)
                    {
                        var align = i < table.Alignments.Count ? table.Alignments[i] : TableBlock.TableAlignment.Left;
                        var alignAttr = align switch
                        {
                            TableBlock.TableAlignment.Center => " align=\"center\"",
                            TableBlock.TableAlignment.Right => " align=\"right\"",
                            _ => ""
                        };
                        sb.Append($"<td{alignAttr}>{EscapeHtml(row[i])}</td>");
                    }
                    sb.Append("</tr>");
                    sb.AppendLine();
                }
                sb.Append("</tbody></table>");
                break;

            case ImageBlock img:
                sb.Append($"<img src=\"{EscapeHtml(img.Url)}\" alt=\"{EscapeHtml(img.Alt ?? "")}\"");
                if (img.Title is not null)
                    sb.Append($" title=\"{EscapeHtml(img.Title)}\"");
                sb.Append(" />");
                break;
        }
    }

    private void RenderInlines(List<Inline> inlines, StringBuilder sb)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline t:
                    sb.Append(EscapeHtml(t.Content));
                    break;
                case BoldInline b:
                    sb.Append("<strong>");
                    RenderInlines(b.Children, sb);
                    sb.Append("</strong>");
                    break;
                case ItalicInline i:
                    sb.Append("<em>");
                    RenderInlines(i.Children, sb);
                    sb.Append("</em>");
                    break;
                case BoldItalicInline bi:
                    sb.Append("<strong><em>");
                    RenderInlines(bi.Children, sb);
                    sb.Append("</em></strong>");
                    break;
                case CodeInline c:
                    sb.Append($"<code>{EscapeHtml(c.Code)}</code>");
                    break;
                case LinkInline l:
                    sb.Append($"<a href=\"{EscapeHtml(l.Url)}\"");
                    if (l.Title is not null)
                        sb.Append($" title=\"{EscapeHtml(l.Title)}\"");
                    sb.Append('>');
                    RenderInlines(l.Children, sb);
                    sb.Append("</a>");
                    break;
                case ImageInline img:
                    sb.Append($"<img src=\"{EscapeHtml(img.Url)}\" alt=\"{EscapeHtml(img.Alt ?? "")}\"");
                    if (img.Title is not null)
                        sb.Append($" title=\"{EscapeHtml(img.Title)}\"");
                    sb.Append(" />");
                    break;
                case StrikethroughInline s:
                    sb.Append("<del>");
                    RenderInlines(s.Children, sb);
                    sb.Append("</del>");
                    break;
                case LineBreakInline:
                    sb.Append("<br />");
                    break;
            }
        }
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
