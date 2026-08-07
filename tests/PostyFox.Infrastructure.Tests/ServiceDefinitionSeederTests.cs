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
    /// FurAffinity's account holds nothing configurable — it authenticates from a handed-over browser
    /// session, and its submission choices belong to the post (see FurAffinityPostOptionsTests). The
    /// seeder re-writes schemas onto existing rows, so this is what an upgrade converges on.
    /// </summary>
    [Fact]
    public void FurAffinity_connector_has_no_account_settings()
    {
        var furAffinity = ServiceDefinitionSeeder.Definitions.Single(d => d.Id == "FurAffinity");

        Assert.Equal("{}", furAffinity.ConfigSchema);
        Assert.Null(furAffinity.SecureConfigSchema);
    }
}
