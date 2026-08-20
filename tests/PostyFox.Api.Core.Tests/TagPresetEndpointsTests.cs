using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Dtos;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class TagPresetEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TagPreset_crud_roundtrip()
    {
        var created = await (await _client.PutAsJsonAsync("/api/tag-presets",
            new TagPresetUpsertRequest(null, "Weekly art", ["art", "fursona", "digital"])))
            .Content.ReadFromJsonAsync<TagPresetDto>();
        Assert.NotNull(created);
        Assert.Equal(["art", "fursona", "digital"], created!.Tags);

        var got = await _client.GetFromJsonAsync<TagPresetDto>($"/api/tag-presets/{created.Id}");
        Assert.Equal("Weekly art", got!.Name);

        var del = await _client.DeleteAsync($"/api/tag-presets/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var missing = await _client.GetAsync($"/api/tag-presets/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
