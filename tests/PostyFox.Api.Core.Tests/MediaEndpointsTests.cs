using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Abstractions;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
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

    [Fact]
    public async Task Limits_reports_the_configured_max_upload_size()
    {
        using var factory = new CustomWebApplicationFactory { MediaMaxUploadSizeBytes = 31_457_280 };
        using var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/media/limits");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var limits = await resp.Content.ReadFromJsonAsync<MediaLimitsDto>();
        Assert.Equal(31_457_280, limits!.MaxUploadSizeBytes);
    }

    [Fact]
    public async Task Limits_reports_null_when_unconfigured()
    {
        var resp = await _client.GetAsync("/api/media/limits");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var limits = await resp.Content.ReadFromJsonAsync<MediaLimitsDto>();
        Assert.Null(limits!.MaxUploadSizeBytes);
    }

    private const string StagedKey = "34c75771-e4c9-4e86-8dd4-1e88fc4cedb7.jpg";

    // DevMode off: the fetcher (Meta) has no session, so the route must work fully anonymous.
    private static WebApplicationFactory<Program> StagedFactory(StagingStore store) =>
        new CustomWebApplicationFactory { DevMode = false }.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IObjectStore>(store)));

    [Fact]
    public async Task Staged_media_is_served_anonymously_with_its_content_type()
    {
        var store = new StagingStore { [(MediaContainers.Staging, StagedKey)] = [1, 2, 3] };
        using var f = StagedFactory(store);

        var resp = await f.CreateClient().GetAsync($"/api/media/staged/{StagedKey}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("image/jpeg", resp.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await resp.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Staged_media_answers_head_requests()
    {
        var store = new StagingStore { [(MediaContainers.Staging, StagedKey)] = [1, 2, 3] };
        using var f = StagedFactory(store);

        var resp = await f.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/api/media/staged/{StagedKey}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("image/jpeg", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Staged_media_that_does_not_exist_is_not_found()
    {
        using var f = StagedFactory(new StagingStore());

        var resp = await f.CreateClient().GetAsync($"/api/media/staged/{StagedKey}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Theory]
    [InlineData("not-a-guid.jpg")]
    [InlineData("34c75771-e4c9-4e86-8dd4-1e88fc4cedb7.txt")]
    [InlineData("34c75771-e4c9-4e86-8dd4-1e88fc4cedb7")]
    [InlineData("..%2Fmedia%2Fdev-user%2Fpic.jpg")]
    public async Task Staged_route_rejects_keys_that_are_not_staged_media(string key)
    {
        // Seeded under the raw key so a 404 proves the shape check, not a missing object.
        var store = new StagingStore { [(MediaContainers.Staging, Uri.UnescapeDataString(key))] = [1] };
        using var f = StagedFactory(store);

        var resp = await f.CreateClient().GetAsync($"/api/media/staged/{key}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>In-memory store that only serves what a test seeds (unlike FakeObjectStore, which stores nothing).</summary>
    private sealed class StagingStore : Dictionary<(string Container, string Key), byte[]>, IObjectStore
    {
        public Task PutAsync(string c, string k, Stream s, string ct, CancellationToken t = default) => Task.CompletedTask;
        public Task PutTextAsync(string c, string k, string content, string ct = "text/plain", CancellationToken t = default) => Task.CompletedTask;
        public Task<Stream> GetAsync(string c, string k, CancellationToken t = default) => Task.FromResult<Stream>(new MemoryStream(this[(c, k)]));
        public Task<string> GetTextAsync(string c, string k, CancellationToken t = default) => Task.FromResult("");
        public Task<bool> ExistsAsync(string c, string k, CancellationToken t = default) => Task.FromResult(ContainsKey((c, k)));
        public Task DeleteAsync(string c, string k, CancellationToken t = default) => Task.CompletedTask;
    }
}
