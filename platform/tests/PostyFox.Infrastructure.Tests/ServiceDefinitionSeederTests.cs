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

        await ServiceDefinitionSeeder.SeedAsync(db.Context); // run again
        Assert.Equal(firstCount, db.Context.ServiceDefinitions.Count());
    }
}
