using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;

namespace PostyFox.Application.Tests.Support;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed record Published(object Message, TimeSpan? Delay);

public sealed class FakeBus : IMessageBus
{
    public List<Published> Messages { get; } = new();
    public IEnumerable<T> Of<T>() => Messages.Select(m => m.Message).OfType<T>();

    public Task PublishAsync<T>(T message, TimeSpan? delay = null, CancellationToken ct = default) where T : class
    {
        Messages.Add(new Published(message, delay));
        return Task.CompletedTask;
    }
}

public sealed class FakeObjectStore : IObjectStore
{
    public Dictionary<string, string> Text { get; } = new();
    public Task PutAsync(string c, string k, Stream s, string ct, CancellationToken t = default) => Task.CompletedTask;
    public Task PutTextAsync(string c, string k, string content, string ct = "text/plain", CancellationToken t = default)
    { Text[$"{c}/{k}"] = content; return Task.CompletedTask; }
    public Task<Stream> GetAsync(string c, string k, CancellationToken t = default) => Task.FromResult<Stream>(new MemoryStream());
    public Task<string> GetTextAsync(string c, string k, CancellationToken t = default) => Task.FromResult(Text.GetValueOrDefault($"{c}/{k}", ""));
    public Task<bool> ExistsAsync(string c, string k, CancellationToken t = default) => Task.FromResult(Text.ContainsKey($"{c}/{k}"));
    public Task DeleteAsync(string c, string k, CancellationToken t = default) { Text.Remove($"{c}/{k}"); return Task.CompletedTask; }
}

public sealed class FakeConnector(string platform, Func<RenderedPost, DeliveryResult>? deliver = null) : IConnector
{
    public int DeliverCount { get; private set; }
    public ConnectorDescriptor Describe() => new(platform, platform, true, false, false, null);
    public Task<AuthState> IsAuthenticatedAsync(ConnectorContext c, CancellationToken t = default) => Task.FromResult(new AuthState(true));
    public Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext c, CancellationToken t = default)
        => Task.FromResult<IReadOnlyList<ConnectorTarget>>([]);
    public Task<DeliveryResult> DeliverAsync(ConnectorContext c, RenderedPost post, CancellationToken t = default)
    { DeliverCount++; return Task.FromResult(deliver?.Invoke(post) ?? DeliveryResult.Ok("ext-1", "http://x/1")); }
}

public sealed class FakeRegistry(params IConnector[] connectors) : IConnectorRegistry
{
    private readonly ConnectorRegistry _inner = new(connectors);
    public bool TryGet(string platform, out IConnector connector) => _inner.TryGet(platform, out connector);
    public IReadOnlyCollection<IConnector> All => _inner.All;
}

public sealed class FakeSecretStore : ISecretsProvider
{
    public Dictionary<string, string> Store { get; } = new();
    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(Store.GetValueOrDefault(key));
    public Task<IDictionary<string, string?>> GetSecretsAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IDictionary<string, string?>>(keys.ToDictionary(k => k, k => Store.GetValueOrDefault(k)));
    public Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default) { Store[key] = value; return Task.CompletedTask; }
    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default) { Store.Remove(key); return Task.CompletedTask; }
    public Task<IEnumerable<string>> ListSecretsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<string>>(Store.Keys.ToList());
}

public sealed class FakeTelegramGateway : ITelegramGateway
{
    public Queue<TelegramLoginStep> Steps { get; } = new();
    public Task<bool> IsAuthenticatedAsync(string u, string p, CancellationToken ct = default) => Task.FromResult(true);
    public Task<IReadOnlyList<ConnectorTarget>> ListChatsAsync(string u, string p, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ConnectorTarget>>([]);
    public Task<DeliveryResult> SendAsync(string u, string p, string c, string b, IReadOnlyList<MediaRef> media, CancellationToken ct = default)
        => Task.FromResult(DeliveryResult.Ok("m"));
    public Task<TelegramLoginStep> LoginAsync(string u, string p, string? v, CancellationToken ct = default)
        => Task.FromResult(Steps.Count > 0 ? Steps.Dequeue() : new TelegramLoginStep(TelegramLoginStep.Complete));
}
