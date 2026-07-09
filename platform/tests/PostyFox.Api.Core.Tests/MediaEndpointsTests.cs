using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Connectors;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class MediaEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upload_returns_media_ref()
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "pic.png");

        var resp = await _client.PostAsync("/api/media", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var media = await resp.Content.ReadFromJsonAsync<MediaRef>();
        Assert.Equal("media", media!.Container);
        Assert.Equal("image/png", media.ContentType);
        Assert.Contains("dev-user", media.Key);
        Assert.EndsWith("pic.png", media.Key);
    }

    [Fact]
    public async Task Upload_without_file_is_bad_request()
    {
        using var form = new MultipartFormDataContent();
        var resp = await _client.PostAsync("/api/media", form);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
