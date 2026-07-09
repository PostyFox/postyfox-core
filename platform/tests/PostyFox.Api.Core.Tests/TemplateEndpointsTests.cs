using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Dtos;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class TemplateEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Template_crud_roundtrip()
    {
        var created = await (await _client.PutAsJsonAsync("/api/templates",
            new TemplateUpsertRequest(null, "Daily", "**Live!** {game}")))
            .Content.ReadFromJsonAsync<TemplateDto>();
        Assert.NotNull(created);

        var got = await _client.GetFromJsonAsync<TemplateDto>($"/api/templates/{created!.Id}");
        Assert.Equal("Daily", got!.Title);

        var del = await _client.DeleteAsync($"/api/templates/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var missing = await _client.GetAsync($"/api/templates/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
