using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PostyFox.Application;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Domain.Entities;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Infrastructure.Secrets;

namespace PostyFox.Worker.Posting.Tests.Support;

/// <summary>Dispatches published messages synchronously to the registered handler (delay ignored).</summary>
public sealed class InProcessBus(IServiceScopeFactory scopes) : IMessageBus
{
    public async Task PublishAsync<T>(T message, TimeSpan? delay = null, CancellationToken ct = default) where T : class
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetService<IMessageHandler<T>>();
        if (handler is not null) await handler.HandleAsync(message, ct);
    }
}

public sealed class FakeObjectStore : IObjectStore
{
    public Task PutAsync(string c, string k, Stream s, string ct, CancellationToken t = default) => Task.CompletedTask;
    public Task PutTextAsync(string c, string k, string content, string ct = "text/plain", CancellationToken t = default) => Task.CompletedTask;
    public Task<Stream> GetAsync(string c, string k, CancellationToken t = default) => Task.FromResult<Stream>(new MemoryStream());
    public Task<string> GetTextAsync(string c, string k, CancellationToken t = default) => Task.FromResult("");
    public Task<bool> ExistsAsync(string c, string k, CancellationToken t = default) => Task.FromResult(false);
    public Task DeleteAsync(string c, string k, CancellationToken t = default) => Task.CompletedTask;
}

public sealed class ProgrammableConnector(string platform, bool succeed) : IConnector
{
    public int Calls { get; private set; }
    public ConnectorDescriptor Describe() => new(platform, platform, true, false, false, null);
    public Task<AuthState> IsAuthenticatedAsync(ConnectorContext c, CancellationToken t = default) => Task.FromResult(new AuthState(true));
    public Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext c, CancellationToken t = default)
        => Task.FromResult<IReadOnlyList<ConnectorTarget>>([]);
    public Task<DeliveryResult> DeliverAsync(ConnectorContext c, RenderedPost post, CancellationToken t = default)
    {
        Calls++;
        return Task.FromResult(succeed ? DeliveryResult.Ok($"ext-{Calls}", "http://x") : DeliveryResult.Fail("boom"));
    }
}

/// <summary>Wires the real pipeline (handlers, template engine, EF/SQLite) with fakes for I/O.</summary>
public sealed class PipelineHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    public ServiceProvider Services { get; }

    public PipelineHarness(params IConnector[] connectors)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IObjectStore, FakeObjectStore>();
        services.AddSingleton<ISecretStore, InMemorySecretStore>();
        services.AddSingleton<IMessageBus, InProcessBus>();
        foreach (var c in connectors) services.AddSingleton(c);
        services.AddSingleton<IEnumerable<IConnector>>(sp => connectors);

        Services = services.BuildServiceProvider();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public async Task<Guid> SeedConnectorAsync(string userId, string platform, bool enabled = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.ServiceDefinitions.Any(s => s.Id == platform))
            db.ServiceDefinitions.Add(new ServiceDefinition { Id = platform, Name = platform, Platform = platform, Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id, UserId = userId, ServiceDefinitionId = platform, DisplayName = platform,
            ConfigJson = "{}", Enabled = enabled
        });
        await db.SaveChangesAsync();
        return id;
    }

    public async Task<T> InScopeAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public void Dispose()
    {
        Services.Dispose();
        _connection.Dispose();
    }
}
