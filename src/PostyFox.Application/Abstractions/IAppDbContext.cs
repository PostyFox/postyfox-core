using Microsoft.EntityFrameworkCore;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Abstractions;

/// <summary>
/// Persistence surface used by the application layer. Implemented by the EF Core
/// <c>AppDbContext</c> in the infrastructure layer.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<ServiceDefinition> ServiceDefinitions { get; }
    DbSet<UserConnector> UserConnectors { get; }
    DbSet<Template> Templates { get; }
    DbSet<ExternalTrigger> ExternalTriggers { get; }
    DbSet<ExternalInterest> ExternalInterests { get; }
    DbSet<Post> Posts { get; }
    DbSet<PostTarget> PostTargets { get; }
    DbSet<WebhookDedupe> WebhookDedupes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
