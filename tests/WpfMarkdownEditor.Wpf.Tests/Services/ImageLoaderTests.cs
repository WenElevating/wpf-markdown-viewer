using System.Net;
using System.Net.Http;
using WpfMarkdownEditor.Wpf.Services;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Services;

public sealed class ImageLoaderTests
{
    [Fact]
    public async Task ResolveImageAsync_ShieldsSvg_RequestsPngVariant()
    {
        var handler = new RecordingHandler();
        using var loader = new ImageLoader(handler);

        var image = await loader.ResolveImageAsync(
            "https://img.shields.io/badge/link-996.icu-red.svg",
            CancellationToken.None);

        Assert.NotNull(image);
        Assert.Equal("png", image.Format);
        Assert.Equal("https://img.shields.io/badge/link-996.icu-red.png", handler.Requests.Single().ToString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47])
            };
            response.Content.Headers.ContentType = new("image/png");
            return Task.FromResult(response);
        }
    }
}
