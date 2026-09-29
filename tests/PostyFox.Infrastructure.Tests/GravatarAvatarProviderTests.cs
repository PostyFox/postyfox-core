using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PostyFox.Infrastructure.Avatars;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class GravatarAvatarProviderTests
{
    private sealed class ImageHandler(HttpStatusCode status, string contentType = "image/png") : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            var content = new ByteArrayContent([1, 2, 3]);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }

    private static GravatarAvatarProvider Provider(HttpMessageHandler handler) =>
        new(new StubHttpClientFactory(handler), new MemoryCache(new MemoryCacheOptions()),
            NullLogger<GravatarAvatarProvider>.Instance);

    [Fact]
    public void Hash_is_sha256_of_trimmed_lowercased_email()
    {
        // Equals `printf %s myemailaddress@example.com | sha256sum`.
        Assert.Equal("84059b07d4be67b806386c0aad8070a23f18836bbaae342275dc0a83414c32ee",
            GravatarAvatarProvider.Hash("  MyEmailAddress@example.com "));
    }

    [Fact]
    public async Task Fetches_with_404_default_and_caches()
    {
        var handler = new ImageHandler(HttpStatusCode.OK);
        var provider = Provider(handler);

        var image = await provider.GetAsync("MyEmailAddress@example.com", 64);
        await provider.GetAsync("myemailaddress@example.com", 64);

        Assert.NotNull(image);
        Assert.Equal("image/png", image!.ContentType);
        Assert.Equal([1, 2, 3], image.Content);
        var uri = Assert.Single(handler.Requests);
        Assert.Equal("gravatar.com", uri.Host);
        Assert.Equal("/avatar/84059b07d4be67b806386c0aad8070a23f18836bbaae342275dc0a83414c32ee", uri.AbsolutePath);
        Assert.Equal("?s=64&d=404", uri.Query);
    }

    [Fact]
    public async Task Missing_avatar_returns_null_and_caches_the_miss()
    {
        var handler = new ImageHandler(HttpStatusCode.NotFound);
        var provider = Provider(handler);

        Assert.Null(await provider.GetAsync("nobody@example.com", 80));
        Assert.Null(await provider.GetAsync("nobody@example.com", 80));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Non_image_response_is_rejected()
    {
        var provider = Provider(new ImageHandler(HttpStatusCode.OK, "text/html"));
        Assert.Null(await provider.GetAsync("someone@example.com", 80));
    }
}
