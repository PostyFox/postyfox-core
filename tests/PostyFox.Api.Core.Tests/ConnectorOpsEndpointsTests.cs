using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Domain.Entities;
using PostyFox.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class ConnectorOpsEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Authenticated_and_targets_dispatch_to_the_real_connector()
    {
        var dto = await (await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "DiscordWH", "Disc", "{\"Webhook\":\"http://x/wh\"}", null, true)))
            .Content.ReadFromJsonAsync<UserConnectorDto>();

        var auth = await _client.GetFromJsonAsync<AuthState>($"/api/connectors/{dto!.Id}/authenticated");
        Assert.True(auth!.IsAuthenticated); // Discord connector: webhook present ⇒ authenticated

        var targets = await _client.GetFromJsonAsync<List<ConnectorTarget>>($"/api/connectors/{dto.Id}/targets");
        Assert.Single(targets!);
    }

    [Fact]
    public async Task Limits_exposes_image_size_cap_from_discord_media_spec()
    {
        var dto = await (await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "DiscordWH", "Disc", "{\"Webhook\":\"http://x/wh\"}", null, true)))
            .Content.ReadFromJsonAsync<UserConnectorDto>();

        var limits = await _client.GetFromJsonAsync<ConnectorLimits>($"/api/connectors/{dto!.Id}/limits");

        Assert.NotNull(limits);
        // Discord's PlatformMediaSpecs.Discord declares MaxBytes = 10 MB for images.
        Assert.Equal(10_485_760, limits!.ImageSizeLimit);
        Assert.Equal(10_485_760, limits.VideoSizeLimit);
        Assert.Equal(10, limits.MaxMediaAttachments);
    }

    [Fact]
    public async Task MediaCheck_returns_will_resize_true_when_image_exceeds_discord_cap()
    {
        var dto = await (await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "DiscordWH", "My Discord", "{\"Webhook\":\"http://x/wh\"}", null, true)))
            .Content.ReadFromJsonAsync<UserConnectorDto>();

        // Discord image cap is 10 MB; send 15 MB.
        var resp = await _client.PostAsJsonAsync("/api/connectors/media-check", new MediaCheckRequest(
            [dto!.Id], FileSize: 15_728_640, MimeType: "image/png"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var results = await resp.Content.ReadFromJsonAsync<List<MediaCheckResultItem>>();
        Assert.Single(results!);
        Assert.True(results![0].WillResize);
        Assert.Equal("My Discord", results[0].DisplayName);
    }

    [Fact]
    public async Task MediaCheck_returns_will_resize_false_when_image_within_discord_cap()
    {
        var dto = await (await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "DiscordWH", "Disc", "{\"Webhook\":\"http://x/wh\"}", null, true)))
            .Content.ReadFromJsonAsync<UserConnectorDto>();

        // Discord image cap is 10 MB; send 1 MB.
        var resp = await _client.PostAsJsonAsync("/api/connectors/media-check", new MediaCheckRequest(
            [dto!.Id], FileSize: 1_048_576, MimeType: "image/jpeg"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var results = await resp.Content.ReadFromJsonAsync<List<MediaCheckResultItem>>();
        Assert.Single(results!);
        Assert.False(results![0].WillResize);
    }

    [Fact]
    public async Task MediaCheck_with_empty_list_returns_empty_array()
    {
        var resp = await _client.PostAsJsonAsync("/api/connectors/media-check", new MediaCheckRequest(
            [], FileSize: 1_000_000, MimeType: "image/jpeg"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var results = await resp.Content.ReadFromJsonAsync<List<MediaCheckResultItem>>();
        Assert.Empty(results!);
    }

    [Fact]
    public async Task Cookie_pairing_connects_furaffinity_and_is_one_use()
    {
        var dto = await (await _client.PutAsJsonAsync("/api/connectors", new UserConnectorUpsertRequest(
            null, "FurAffinity", "FA", "{}", null, true)))
            .Content.ReadFromJsonAsync<UserConnectorDto>();

        var startResponse = await _client.PostAsync(
            $"/api/connectors/{dto!.Id}/cookie-pairing/start",
            content: null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var start = await startResponse.Content.ReadFromJsonAsync<ConnectorCookiePairingStart>();
        Assert.NotNull(start);

        var payload = new
        {
            pairingToken = start!.PairingToken,
            cookies = new Dictionary<string, string>
            {
                ["a"] = "session-a",
                ["b"] = "session-b"
            }
        };
        var complete = await _client.PostAsJsonAsync("/api/connectors/cookie-pairing/complete", payload);
        Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);

        var replay = await _client.PostAsJsonAsync("/api/connectors/cookie-pairing/complete", payload);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Cookie_pairing_completion_is_anonymous()
    {
        using var unauthenticatedFactory = new CustomWebApplicationFactory { DevMode = false };
        using var client = unauthenticatedFactory.CreateClient();

        ConnectorCookiePairingStart start;
        using (var scope = unauthenticatedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connectorId = Guid.NewGuid();
            db.UserConnectors.Add(new UserConnector
            {
                Id = connectorId,
                UserId = "owner",
                ServiceDefinitionId = "FurAffinity",
                DisplayName = "FA",
                ConfigJson = "{}",
                Enabled = true
            });
            await db.SaveChangesAsync();
            start = (await scope.ServiceProvider
                .GetRequiredService<ConnectorCookiePairingService>()
                .StartAsync("owner", connectorId))!;
        }

        var response = await client.PostAsJsonAsync("/api/connectors/cookie-pairing/complete", new
        {
            pairingToken = start.PairingToken,
            cookies = new Dictionary<string, string>
            {
                ["a"] = "session-a",
                ["b"] = "session-b"
            }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
