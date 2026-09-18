using System.Text.Json;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// Toyhouse's per-upload choices ship as an embedded resource rather than a C# literal, so a missing
/// or malformed file would otherwise only surface at boot. These cover the file's content and the
/// guarantees the compose form relies on; the wiring onto the descriptor and out through /api/services
/// is covered by ServiceEndpointsTests.
/// </summary>
public class ToyhousePostOptionsTests
{
    private const string FileName = "toyhouse-post-options.schema.json";
    private static readonly string[] ChoiceFields = ["AuthorizedViewers", "PublicViewers", "Watermark"];
    private static readonly string[] RequiredFields = ["CharacterIds", "ArtistName"];

    [Fact]
    public void Schema_loads_and_is_compacted()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.DoesNotContain('\n', schema); // the on-disk formatting is for review only
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(schema).RootElement.ValueKind);
    }

    [Fact]
    public void Every_id_field_offers_named_choices()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));
        var root = doc.RootElement;

        foreach (var field in ChoiceFields)
        {
            var options = root.GetProperty(field).GetProperty("options");
            Assert.True(options.GetArrayLength() > 1, $"{field} should offer a list of choices");
            Assert.All(options.EnumerateArray(), option =>
            {
                Assert.NotEmpty(option.GetProperty("value").GetString()!);
                Assert.NotEmpty(option.GetProperty("label").GetString()!);
            });
        }

        // Characters belong to an individual account, so that one stays free text.
        Assert.False(root.GetProperty("CharacterIds").TryGetProperty("options", out _));
    }

    /// <summary>
    /// The Node connector falls back to authorized_privacy=0, public_privacy=0, watermark_id=1 when a
    /// field is left unset (see toyhouse.ts `privacyOption`/`watermarkOption`). Those must be real,
    /// selectable choices or the dropdown's "leave unset" label would be a lie.
    /// </summary>
    [Fact]
    public void Connector_fallback_values_are_selectable()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));
        var root = doc.RootElement;

        var expected = new Dictionary<string, string>
        {
            ["AuthorizedViewers"] = "0",
            ["PublicViewers"] = "0",
            ["Watermark"] = "1"
        };
        foreach (var (field, fallback) in expected)
            Assert.Contains(
                fallback,
                root.GetProperty(field).GetProperty("options").EnumerateArray()
                    .Select(o => o.GetProperty("value").GetString()));
    }

    [Fact]
    public void Required_fields_are_declared()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));
        var root = doc.RootElement;

        foreach (var field in RequiredFields)
            Assert.True(root.GetProperty(field).GetProperty("required").GetBoolean(), $"{field} should be required");
    }

    [Fact]
    public void Choices_and_requirements_are_enforced_and_metadata_is_not_a_field()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.Contains("$comment", schema);
        Assert.Equal(
            "Character IDs is required.",
            ConfigSchemaValidator.Validate(schema, "{}"));
        Assert.Null(ConfigSchemaValidator.Validate(
            schema, """{"CharacterIds":"111, 222","ArtistName":"FoxArtist","Watermark":"2"}"""));
        Assert.Equal(
            "Character IDs must be comma-separated numbers.",
            ConfigSchemaValidator.Validate(
                schema, """{"CharacterIds":"not-a-number","ArtistName":"FoxArtist"}"""));
        Assert.Equal(
            "Watermark style is not one of the available choices.",
            ConfigSchemaValidator.Validate(
                schema, """{"CharacterIds":"111","ArtistName":"FoxArtist","Watermark":"999"}"""));
    }
}
