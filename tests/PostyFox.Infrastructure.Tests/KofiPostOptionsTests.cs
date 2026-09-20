using System.Text.Json;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// Ko-fi's per-post audience choice ships as an embedded resource, so a missing or malformed file
/// would otherwise only surface at boot. Wiring onto the descriptor is covered by ServiceEndpointsTests.
/// </summary>
public class KofiPostOptionsTests
{
    private const string FileName = "kofi-post-options.schema.json";

    [Fact]
    public void Schema_loads_and_is_compacted()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.DoesNotContain('\n', schema);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(schema).RootElement.ValueKind);
    }

    [Fact]
    public void Audience_offers_the_sites_three_audiences()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));

        var values = doc.RootElement.GetProperty("Audience").GetProperty("options")
            .EnumerateArray().Select(o => o.GetProperty("value").GetString());

        Assert.Equal(["public", "supporter", "recurringSupporter"], values);
    }
}
