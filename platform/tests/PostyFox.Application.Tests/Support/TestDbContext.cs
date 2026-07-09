using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Tests.Support;

/// <summary>EF Core in-memory context implementing IAppDbContext for service unit tests.</summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ServiceDefinition> ServiceDefinitions => Set<ServiceDefinition>();
    public DbSet<UserConnector> UserConnectors => Set<UserConnector>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<ExternalTrigger> ExternalTriggers => Set<ExternalTrigger>();
    public DbSet<ExternalInterest> ExternalInterests => Set<ExternalInterest>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostTarget> PostTargets => Set<PostTarget>();
    public DbSet<WebhookDedupe> WebhookDedupes => Set<WebhookDedupe>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ExternalInterest>().HasKey(x => new { x.SourceType, x.ExternalAccount, x.UserId });
        b.Entity<WebhookDedupe>().HasKey(x => new { x.Source, x.MessageId });
        b.Entity<UserConnector>().HasOne(x => x.ServiceDefinition).WithMany().HasForeignKey(x => x.ServiceDefinitionId);
    }

    public static TestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        return new TestDbContext(options);
    }
}
