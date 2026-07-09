using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Dtos;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class ProfileEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_list_and_revoke_api_key()
    {
        var create = await _client.PostAsJsonAsync("/api/profile/keys", new { name = "ci" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ApiKeyCreatedDto>();
        Assert.NotNull(created);
        Assert.Equal(40, created!.ApiKey.Length);

        var list = await _client.GetFromJsonAsync<List<ApiKeyDto>>("/api/profile/keys");
        Assert.Contains(list!, k => k.Id == created.Id);

        var revoke = await _client.DeleteAsync($"/api/profile/keys/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
    }

    [Fact]
    public async Task Created_key_authenticates_via_api_key_header()
    {
        var created = await (await _client.PostAsJsonAsync("/api/profile/keys", new { name = "ext" }))
            .Content.ReadFromJsonAsync<ApiKeyCreatedDto>();

        using var external = factory.CreateClient();
        external.DefaultRequestHeaders.Add("X-API-Key", created!.ApiKey);
        var resp = await external.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
