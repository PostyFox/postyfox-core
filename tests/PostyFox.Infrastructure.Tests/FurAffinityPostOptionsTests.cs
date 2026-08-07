using System.Text.Json;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// FurAffinity's per-submission choices ship as an embedded resource rather than a C# literal, so a
/// missing or malformed file would otherwise only surface at boot. These cover the file's content and
/// the guarantees the compose form relies on; the wiring onto the descriptor and out through
/// /api/services is covered by ServiceEndpointsTests.
/// </summary>
public class FurAffinityPostOptionsTests
{
    private const string FileName = "furaffinity-post-options.schema.json";
    private static readonly string[] ChoiceFields = ["Category", "Theme", "Species", "Gender"];

    [Fact]
    public void Schema_loads_and_is_compacted()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.DoesNotContain('\n', schema); // the on-disk formatting is for review only
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(schema).RootElement.ValueKind);
    }

    [Fact]
    public void Missing_schema_fails_loudly_rather_than_silently()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => EmbeddedSchema.Load("nope.schema.json"));
        Assert.Contains("nope.schema.json", ex.Message);
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

        // Gallery folders belong to an individual account, so that one stays free text.
        Assert.False(root.GetProperty("FolderIds").TryGetProperty("options", out _));
    }

    /// <summary>
    /// The Node connector falls back to cat=1, atype=1, species=1, gender=0 when a field is left unset
    /// (see furaffinity.ts `numericOption`). Those must be real, selectable choices or the dropdown's
    /// "leave unset" label would be a lie.
    /// </summary>
    [Fact]
    public void Connector_fallback_values_are_selectable()
    {
        using var doc = JsonDocument.Parse(EmbeddedSchema.Load(FileName));
        var root = doc.RootElement;

        var expected = new Dictionary<string, string>
        {
            ["Category"] = "1",
            ["Theme"] = "1",
            ["Species"] = "1",
            ["Gender"] = "0"
        };
        foreach (var (field, fallback) in expected)
            Assert.Contains(
                fallback,
                root.GetProperty(field).GetProperty("options").EnumerateArray()
                    .Select(o => o.GetProperty("value").GetString()));
    }

    [Fact]
    public void Choices_are_enforced_and_metadata_is_not_a_field()
    {
        var schema = EmbeddedSchema.Load(FileName);

        Assert.Contains("$comment", schema);
        // Every field is optional, so an empty selection is valid — in particular $comment must not be
        // validated as a required field.
        Assert.Null(ConfigSchemaValidator.Validate(schema, "{}"));
        Assert.Null(ConfigSchemaValidator.Validate(schema, """{"Category":"13","Gender":"2"}"""));
        Assert.Equal(
            "Category is not one of the available choices.",
            ConfigSchemaValidator.Validate(schema, """{"Category":"999999"}"""));
        Assert.Equal(
            "Folder IDs must be comma-separated numbers.",
            ConfigSchemaValidator.Validate(schema, """{"FolderIds":"not-a-number"}"""));
    }
}
