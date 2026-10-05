using System.Text.Json;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// SoFurry's per-submission choices ship as an embedded resource, so a missing or malformed file
/// would otherwise only surface at boot. Wiring onto the descriptor is covered by ServiceEndpointsTests.
/// </summary>
public class SofurryPostOptionsTests
{
    private const string FileName = "sofurry-post-options.schema.json";

    [Fact]
    public void Schema_loads_and_is_compacted()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.DoesNotContain('\n', schema);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(schema).RootElement.ValueKind);
    }

    [Fact]
    public void Types_are_the_APIs_image_type_ids()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));

        var values = doc.RootElement.GetProperty("Type").GetProperty("options")
            .EnumerateArray().Select(o => o.GetProperty("value").GetString());

        // The connector derives the category from the type id (31 → 30), so every id must sit in an
        // image category: Artwork (1x) or Photography (3x).
        Assert.Equal(["11", "12", "13", "19", "31", "32", "39"], values);
    }

    [Fact]
    public void Visibility_offers_the_non_public_choices()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));

        var values = doc.RootElement.GetProperty("Privacy").GetProperty("options")
            .EnumerateArray().Select(o => o.GetProperty("value").GetString());

        Assert.Equal(["2", "1"], values);
    }
}
