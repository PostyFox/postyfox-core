using System.Collections.Concurrent;
using PostyFox.Application.Abstractions;

namespace PostyFox.Infrastructure.Secrets;

/// <summary>Non-persistent secret store for tests and local ephemeral runs.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public Task SetSecretAsync(string name, string value, CancellationToken ct = default)
    {
        _store[name] = value;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(name, out var v) ? v : null);

    public Task DeleteSecretAsync(string name, CancellationToken ct = default)
    {
        _store.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
