using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Domain.Entities;

namespace PostyFox.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
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
    public DbSet<SecretEntry> Secrets => Set<SecretEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e => { e.ToTable("users"); e.HasKey(x => x.Id); });

        b.Entity<ApiKey>(e =>
        {
            e.ToTable("api_keys");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Prefix);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Prefix).HasMaxLength(16);
        });

        b.Entity<ServiceDefinition>(e =>
        {
            e.ToTable("service_definitions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
        });

        b.Entity<UserConnector>(e =>
        {
            e.ToTable("user_connectors");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.ServiceDefinition)
                .WithMany()
                .HasForeignKey(x => x.ServiceDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Template>(e =>
        {
            e.ToTable("templates");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<ExternalTrigger>(e =>
        {
            e.ToTable("external_triggers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SourceType, x.ExternalAccount });
        });

        b.Entity<ExternalInterest>(e =>
        {
            e.ToTable("external_interests");
            e.HasKey(x => new { x.SourceType, x.ExternalAccount, x.UserId });
        });

        b.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.RootStatus).HasConversion<string>().HasMaxLength(32);
            e.HasMany(x => x.Targets).WithOne(x => x.Post!).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PostTarget>(e =>
        {
            e.ToTable("post_targets");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PostId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        b.Entity<WebhookDedupe>(e =>
        {
            e.ToTable("webhook_dedupe");
            e.HasKey(x => new { x.Source, x.MessageId });
        });

        b.Entity<SecretEntry>(e =>
        {
            e.ToTable("secrets");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(256);
        });
    }
}
