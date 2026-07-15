using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Neillans.Adapters.Secrets.Core;
using Neillans.Adapters.Secrets.InMemory;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Messaging;
using PostyFox.Domain.Entities;
using PostyFox.Infrastructure.Persistence;

namespace PostyFox.Api.Post.Tests.Support;

public sealed class FakeObjectStore : IObjectStore
{
    public ConcurrentDictionary<string, string> Text { get; } = new();
    public Task PutAsync(string c, string k, Stream s, string ct, CancellationToken t = default) => Task.CompletedTask;
    public Task PutTextAsync(string c, string k, string content, string ct = "text/plain", CancellationToken t = default)
    { Text[$"{c}/{k}"] = content; return Task.CompletedTask; }
    public Task<Stream> GetAsync(string c, string k, CancellationToken t = default) => Task.FromResult<Stream>(new MemoryStream());
    public Task<string> GetTextAsync(string c, string k, CancellationToken t = default) => Task.FromResult("");
    public Task<bool> ExistsAsync(string c, string k, CancellationToken t = default) => Task.FromResult(false);
    public Task DeleteAsync(string c, string k, CancellationToken t = default) => Task.CompletedTask;
}

public sealed class FakeBus : IMessageBus
{
    public ConcurrentBag<object> Messages { get; } = new();
    public Task PublishAsync<T>(T message, TimeSpan? delay = null, CancellationToken ct = default) where T : class
    { Messages.Add(message); return Task.CompletedTask; }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    public Guid SeededConnectorId { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:DevMode"] = "true",
            ["Auth:DevUserId"] = "dev-user"
        }));

        builder.ConfigureTestServices(services =>
        {
            RemoveEfCore(services);
            Remove<IObjectStore>(services);
            Remove<IMessageBus>(services);
            Remove<ISecretsProvider>(services);

            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
            services.AddSingleton<IObjectStore, FakeObjectStore>();
            services.AddSingleton<IMessageBus, FakeBus>();
            services.AddInMemorySecretsProvider();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = "DiscordWH", Name = "Discord", Platform = "DiscordWH", Enabled = true });
        db.UserConnectors.Add(new UserConnector
        {
            Id = SeededConnectorId, UserId = "dev-user", ServiceDefinitionId = "DiscordWH",
            DisplayName = "My Discord", ConfigJson = "{\"Webhook\":\"http://x\"}", Enabled = true
        });
        db.SaveChanges();
        return host;
    }

    public FakeBus Bus => (FakeBus)Services.GetRequiredService<IMessageBus>();

    private static void Remove<T>(IServiceCollection services)
    {
        foreach (var d in services.Where(x => x.ServiceType == typeof(T)).ToList()) services.Remove(d);
    }

    private static void RemoveEfCore(IServiceCollection services)
    {
        var toRemove = services.Where(d =>
            d.ServiceType == typeof(AppDbContext) ||
            d.ServiceType == typeof(DbContextOptions) ||
            (d.ServiceType.IsGenericType &&
             d.ServiceType.GetGenericArguments() is [var arg] && arg == typeof(AppDbContext)))
            .ToList();
        foreach (var d in toRemove) services.Remove(d);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
