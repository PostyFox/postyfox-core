using System.Text.Json;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// DeviantArt's per-submission choices ship as an embedded resource, so a missing or malformed file
/// would otherwise only surface at boot. Wiring onto the descriptor is covered by ServiceEndpointsTests.
/// </summary>
public class DeviantartPostOptionsTests
{
    private const string FileName = "deviantart-post-options.schema.json";

    [Fact]
    public void Schema_loads_and_is_compacted()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.DoesNotContain('\n', schema);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(schema).RootElement.ValueKind);
    }

    [Fact]
    public void Mode_offers_only_stash_beside_the_publish_default()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));

        var values = doc.RootElement.GetProperty("Mode").GetProperty("options")
            .EnumerateArray().Select(o => o.GetProperty("value").GetString());

        // The connector rejects any other mode value.
        Assert.Equal(["stash"], values);
    }
}
