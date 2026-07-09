using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
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
}
