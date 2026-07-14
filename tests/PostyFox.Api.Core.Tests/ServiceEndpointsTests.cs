using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Dtos;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class ServiceEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Catalog_lists_seeded_definition()
    {
        var defs = await _client.GetFromJsonAsync<List<ServiceDefinitionDto>>("/api/services");
        Assert.Contains(defs!, d => d.Id == "DiscordWH");
    }

    [Fact]
    public async Task Catalog_exposes_connector_capabilities()
    {
        var defs = await _client.GetFromJsonAsync<List<ServiceDefinitionDto>>("/api/services");
        var discord = Assert.Single(defs!, d => d.Id == "DiscordWH");
        // Capabilities are surfaced from DiscordWebhookConnector.Describe().
        Assert.True(discord.SupportsTitle);
        Assert.True(discord.SupportsMedia);
        Assert.False(discord.SupportsThreads);
        Assert.Equal(2000, discord.MaxContentLength);
    }

    [Fact]
    public async Task Connector_upsert_get_delete_roundtrip()
    {
        var upsert = await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "DiscordWH", "My Discord", "{\"Webhook\":\"http://x\"}", "{\"secret\":\"s\"}", true));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var dto = await upsert.Content.ReadFromJsonAsync<UserConnectorDto>();
        Assert.Equal("DiscordWH", dto!.Platform);

        var got = await _client.GetFromJsonAsync<UserConnectorDto>($"/api/connectors/{dto.Id}");
        Assert.Equal(dto.Id, got!.Id);

        var del = await _client.DeleteAsync($"/api/connectors/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [Fact]
    public async Task Connector_upsert_unknown_definition_is_bad_request()
    {
        var resp = await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "DoesNotExist", "x", "{}", null, true));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
