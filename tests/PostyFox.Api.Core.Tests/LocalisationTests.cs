using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PostyFox.Api.Core.Tests.Support;
using Xunit;

namespace PostyFox.Api.Core.Tests;

/// <summary>Issue #33: API messages come from the localised resources, chosen by Accept-Language.</summary>
public class LocalisationTests
{
    [Theory]
    [InlineData("en-GB")]
    [InlineData("fr-FR")] // no translation yet: falls back to en-GB
    [InlineData(null)]
    public async Task Error_messages_use_the_requested_or_default_language(string? acceptLanguage)
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        // Thrown in the Application layer, so this also covers culture flowing into services.
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/text-templates")
        {
            Content = JsonContent.Create(new { name = "", defaultValue = "" }),
        };
        if (acceptLanguage is not null) request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Text template name is required.", body.GetProperty("error").GetString());
    }
}
