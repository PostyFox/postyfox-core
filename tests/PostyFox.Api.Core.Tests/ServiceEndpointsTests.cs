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

        var bluesky = Assert.Single(defs!, d => d.Id == "BlueSky");
        Assert.True(bluesky.SupportsRating);
        Assert.False(bluesky.RequiresRating);
        Assert.Null(bluesky.PostOptionsSchema); // nothing to choose per submission
        // Bluesky has no click-to-reveal content warning mechanism.
        Assert.False(bluesky.SupportsContentWarning);

        var furAffinity = Assert.Single(defs!, d => d.Id == "FurAffinity");
        Assert.True(furAffinity.SupportsCookiePairing);
        Assert.True(furAffinity.SupportsRating);
        Assert.True(furAffinity.RequiresRating);
        Assert.True(furAffinity.SupportsTextOnly);
        Assert.Null(furAffinity.SecureConfigSchema);
        // The FurAffinity account itself carries no settings: category/species/gender/folders are
        // chosen per submission, so they reach the compose form as post options, not connector config.
        Assert.Equal("{}", furAffinity.ConfigSchema);
        Assert.NotNull(furAffinity.PostOptionsSchema);
        Assert.Contains("\"Species\"", furAffinity.PostOptionsSchema);
        Assert.False(furAffinity.SupportsContentWarning);

        var toyhouse = Assert.Single(defs!, d => d.Id == "Toyhouse");
        Assert.True(toyhouse.SupportsCookiePairing);
        Assert.True(toyhouse.SupportsRating);
        Assert.True(toyhouse.RequiresRating);
        Assert.True(toyhouse.RequiresMedia);
        Assert.False(toyhouse.SupportsTitle); // images attach to character pages, not their own title
        Assert.False(toyhouse.SupportsTags); // no native tags field
        Assert.Null(toyhouse.SecureConfigSchema);
        Assert.Equal("{}", toyhouse.ConfigSchema);
        Assert.NotNull(toyhouse.PostOptionsSchema);
        Assert.Contains("\"CharacterIds\"", toyhouse.PostOptionsSchema);

        // Every Fediverse platform supports a click-to-reveal content warning, authored per submission
        // (never the post title), see megalodon.ts.
        var mastodon = Assert.Single(defs!, d => d.Id == "Mastodon");
        Assert.True(mastodon.SupportsContentWarning);
        Assert.NotNull(mastodon.PostOptionsSchema);
        Assert.Contains("\"ContentWarning\"", mastodon.PostOptionsSchema);
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
