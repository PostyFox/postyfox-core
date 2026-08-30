using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Dtos;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class TextTemplateEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TextTemplate_crud_roundtrip()
    {
        var connectorId = Guid.NewGuid();
        var created = await (await _client.PutAsJsonAsync("/api/text-templates",
            new TextTemplateUpsertRequest(null, "mention", "friend", new Dictionary<Guid, string> { [connectorId] = "@alice" })))
            .Content.ReadFromJsonAsync<TextTemplateDto>();
        Assert.NotNull(created);
        Assert.Equal("mention", created!.Name);
        Assert.Equal("friend", created.DefaultValue);
        Assert.Equal("@alice", created.ConnectorValues[connectorId]);

        var got = await _client.GetFromJsonAsync<TextTemplateDto>($"/api/text-templates/{created.Id}");
        Assert.Equal("mention", got!.Name);

        var del = await _client.DeleteAsync($"/api/text-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var missing = await _client.GetAsync($"/api/text-templates/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task TextTemplate_upsert_rejects_a_duplicate_name_with_bad_request()
    {
        await _client.PutAsJsonAsync("/api/text-templates",
            new TextTemplateUpsertRequest(null, "dup", "", new Dictionary<Guid, string>()));

        var resp = await _client.PutAsJsonAsync("/api/text-templates",
            new TextTemplateUpsertRequest(null, "dup", "", new Dictionary<Guid, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
