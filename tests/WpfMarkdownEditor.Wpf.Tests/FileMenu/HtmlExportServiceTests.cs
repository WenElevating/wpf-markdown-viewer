using WpfMarkdownEditor.Sample;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class HtmlExportServiceTests
{
    [Fact]
    public void ExportHtml_RendersBlocksAndInlines()
    {
        var service = new HtmlExportService();
        var markdown = "# Title\n\nHello **bold** and [link](https://example.com).\n\n- One\n- Two";

        var html = service.ExportHtml(markdown, "Document");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<h1>Title</h1>", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<a href=\"https://example.com\">link</a>", html);
        Assert.Contains("<ul>", html);
    }

    [Fact]
    public void ExportHtml_EncodesTextAndAttributes()
    {
        var service = new HtmlExportService();
        var html = service.ExportHtml("# <script>\n\n[bad](https://example.com/?q=<tag>)", "A & B");

        Assert.Contains("<title>A &amp; B</title>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("https://example.com/?q=&lt;tag&gt;", html);
    }
}
