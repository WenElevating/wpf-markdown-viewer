using System.IO;
using System.Net;
using System.Text;
using WpfMarkdownEditor.Core.Parsing;
using WpfMarkdownEditor.Core.Parsing.Blocks;
using WpfMarkdownEditor.Core.Parsing.Inlines;

namespace WpfMarkdownEditor.Sample;

public sealed class HtmlExportService
{
    private readonly MarkdownParser _parser = new();

    public string ExportHtml(string markdown, string title)
    {
        var blocks = _parser.Parse(markdown);
        var body = new StringBuilder();
        foreach (var block in blocks)
            RenderBlock(body, block);

        return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{{Encode(title)}}</title>
<style>
body { font-family: "Segoe UI", sans-serif; line-height: 1.6; max-width: 900px; margin: 40px auto; padding: 0 24px; color: #24292f; }
pre { background: #f6f8fa; padding: 12px; overflow: auto; }
code { background: #f6f8fa; padding: 0.1em 0.3em; border-radius: 3px; }
pre code { padding: 0; background: transparent; }
table { border-collapse: collapse; width: 100%; }
th, td { border: 1px solid #d0d7de; padding: 6px 8px; }
blockquote { border-left: 4px solid #d0d7de; margin-left: 0; padding-left: 16px; color: #57606a; }
</style>
</head>
<body>
{{body}}
</body>
</html>
""";
    }

    public void ExportHtmlToFile(string markdown, string title, string path)
    {
        File.WriteAllText(path, ExportHtml(markdown, title), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void RenderBlock(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var level = Math.Clamp(heading.Level, 1, 6);
                sb.Append("<h").Append(level).Append('>');
                RenderInlines(sb, heading.Inlines);
                sb.Append("</h").Append(level).AppendLine(">");
                break;
            case ParagraphBlock paragraph:
                sb.Append("<p>");
                RenderInlines(sb, paragraph.Inlines);
                sb.AppendLine("</p>");
                break;
            case BlockquoteBlock quote:
                sb.AppendLine("<blockquote>");
                foreach (var child in quote.Children)
                    RenderBlock(sb, child);
                sb.AppendLine("</blockquote>");
                break;
            case CodeBlock code:
                sb.Append("<pre><code");
                if (!string.IsNullOrWhiteSpace(code.Language))
                    sb.Append(" class=\"language-").Append(Encode(code.Language)).Append('"');
                sb.Append('>').Append(Encode(code.Code)).AppendLine("</code></pre>");
                break;
            case ListBlock list:
                RenderList(sb, list);
                break;
            case TableBlock table:
                RenderTable(sb, table);
                break;
            case ThematicBreakBlock:
                sb.AppendLine("<hr>");
                break;
            case ImageBlock image:
                sb.Append("<p><img src=\"").Append(Encode(image.Url)).Append("\" alt=\"").Append(Encode(image.Alt ?? "")).Append('"');
                if (!string.IsNullOrWhiteSpace(image.Title))
                    sb.Append(" title=\"").Append(Encode(image.Title)).Append('"');
                sb.AppendLine("></p>");
                break;
        }
    }

    private static void RenderList(StringBuilder sb, ListBlock list)
    {
        var tag = list.IsOrdered ? "ol" : "ul";
        sb.Append('<').Append(tag);
        if (list.IsOrdered && list.StartNumber != 1)
            sb.Append(" start=\"").Append(list.StartNumber).Append('"');
        sb.AppendLine(">");
        foreach (var item in list.Items)
        {
            sb.AppendLine("<li>");
            foreach (var child in item.Blocks)
                RenderBlock(sb, child);
            sb.AppendLine("</li>");
        }

        sb.Append("</").Append(tag).AppendLine(">");
    }

    private static void RenderTable(StringBuilder sb, TableBlock table)
    {
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr>");
        foreach (var header in table.Headers)
            sb.Append("<th>").Append(Encode(header)).AppendLine("</th>");
        sb.AppendLine("</tr></thead>");
        sb.AppendLine("<tbody>");
        foreach (var row in table.Rows)
        {
            sb.AppendLine("<tr>");
            foreach (var cell in row)
                sb.Append("<td>").Append(Encode(cell)).AppendLine("</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");
    }

    private static void RenderInlines(StringBuilder sb, IEnumerable<Inline> inlines)
    {
        foreach (var inline in inlines)
            RenderInline(sb, inline);
    }

    private static void RenderInline(StringBuilder sb, Inline inline)
    {
        switch (inline)
        {
            case TextInline text:
                sb.Append(Encode(text.Content));
                break;
            case BoldInline bold:
                sb.Append("<strong>");
                RenderInlines(sb, bold.Children);
                sb.Append("</strong>");
                break;
            case ItalicInline italic:
                sb.Append("<em>");
                RenderInlines(sb, italic.Children);
                sb.Append("</em>");
                break;
            case BoldItalicInline boldItalic:
                sb.Append("<strong><em>");
                RenderInlines(sb, boldItalic.Children);
                sb.Append("</em></strong>");
                break;
            case StrikethroughInline strike:
                sb.Append("<del>");
                RenderInlines(sb, strike.Children);
                sb.Append("</del>");
                break;
            case CodeInline code:
                sb.Append("<code>").Append(Encode(code.Code)).Append("</code>");
                break;
            case LinkInline link:
                sb.Append("<a href=\"").Append(Encode(link.Url)).Append('"');
                if (!string.IsNullOrWhiteSpace(link.Title))
                    sb.Append(" title=\"").Append(Encode(link.Title)).Append('"');
                sb.Append('>');
                RenderInlines(sb, link.Children);
                sb.Append("</a>");
                break;
            case ImageInline image:
                sb.Append("<img src=\"").Append(Encode(image.Url)).Append("\" alt=\"").Append(Encode(image.Alt ?? "")).Append('"');
                if (!string.IsNullOrWhiteSpace(image.Title))
                    sb.Append(" title=\"").Append(Encode(image.Title)).Append('"');
                sb.Append('>');
                break;
            case LineBreakInline:
                sb.Append("<br>");
                break;
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
