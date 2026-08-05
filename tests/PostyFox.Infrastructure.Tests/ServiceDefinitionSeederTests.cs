using System.Text.Json;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class ServiceDefinitionSeederTests
{
    [Fact]
    public async Task Seed_populates_catalogue_and_is_idempotent()
    {
        using var db = new SqliteDb();

        await ServiceDefinitionSeeder.SeedAsync(db.Context);
        var firstCount = db.Context.ServiceDefinitions.Count();
        Assert.Equal(ServiceDefinitionSeeder.Definitions.Length, firstCount);
        Assert.Contains(db.Context.ServiceDefinitions, s => s.Id == "DiscordWH");
        Assert.Contains(db.Context.ServiceDefinitions, s =>
            s.Id == "FurAffinity" && s.SecureConfigSchema == null);

        await ServiceDefinitionSeeder.SeedAsync(db.Context); // run again
        Assert.Equal(firstCount, db.Context.ServiceDefinitions.Count());
    }

    /// <summary>
    /// The FurAffinity schema is an embedded resource rather than a literal, so a missing or malformed
    /// file would only surface at boot. Assert it loads, compacts, and carries usable choice lists.
    /// </summary>
    [Fact]
    public void FurAffinity_schema_offers_named_choices_for_every_id_field()
    {
        var schema = ServiceDefinitionSeeder.Definitions.Single(d => d.Id == "FurAffinity").ConfigSchema;

        Assert.DoesNotContain('\n', schema); // Minified() stripped the on-disk formatting
        using var doc = JsonDocument.Parse(schema);
        var root = doc.RootElement;

        foreach (var field in new[] { "Category", "Theme", "Species", "Gender" })
        {
            var options = root.GetProperty(field).GetProperty("options");
            Assert.True(options.GetArrayLength() > 1, $"{field} should offer a list of choices");
            Assert.All(options.EnumerateArray(), option =>
            {
                Assert.NotEmpty(option.GetProperty("value").GetString()!);
                Assert.NotEmpty(option.GetProperty("label").GetString()!);
            });
        }

        // The defaults the FurAffinity connector falls back to must be selectable.
        Assert.Equal(
            ["1", "1", "1", "0"],
            new[] { "Category", "Theme", "Species", "Gender" }.Select(field =>
                root.GetProperty(field).GetProperty("options").EnumerateArray()
                    .Select(o => o.GetProperty("value").GetString())
                    .First(v => v is "1" or "0")));

        // Folders belong to an individual account, so that field stays free text.
        Assert.False(root.GetProperty("FolderIds").TryGetProperty("options", out _));
    }

    [Fact]
    public void FurAffinity_schema_metadata_is_not_treated_as_a_field()
    {
        var schema = ServiceDefinitionSeeder.Definitions.Single(d => d.Id == "FurAffinity").ConfigSchema;

        Assert.Contains("$comment", schema);
        // A saved connector supplying nothing but valid choices must pass — in particular, $comment
        // must not be validated as a required field.
        Assert.Null(ConfigSchemaValidator.Validate(schema, """{"Category":"13","Gender":"2"}"""));
        Assert.Equal(
            "Category is not one of the available choices.",
            ConfigSchemaValidator.Validate(schema, """{"Category":"999999"}"""));
    }
}
