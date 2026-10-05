using System.Text.Json;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// Artconomy's per-submission choices ship as an embedded resource, so a missing or malformed file
/// would otherwise only surface at boot. Wiring onto the descriptor is covered by ServiceEndpointsTests.
/// </summary>
public class ArtconomyPostOptionsTests
{
    private const string FileName = "artconomy-post-options.schema.json";

    [Fact]
    public void Schema_loads_and_is_compacted()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.DoesNotContain('\n', schema);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(schema).RootElement.ValueKind);
    }

    [Theory]
    [InlineData("CreditAsArtist", "false")]
    [InlineData("Private", "true")]
    [InlineData("DisableComments", "true")]
    public void Each_option_offers_only_the_non_default_choice(string field, string value)
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));

        var values = doc.RootElement.GetProperty(field).GetProperty("options")
            .EnumerateArray().Select(o => o.GetProperty("value").GetString());

        Assert.Equal([value], values);
    }
}
