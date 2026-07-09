using PostyFox.Application.Abstractions;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// MTProto session stream persisted to the object store (port of the legacy blob-backed
/// TelegramStore). Loads existing session on open; flushes changes back to object storage.
/// </summary>
public sealed class BlobSessionStore : MemoryStream
{
    private readonly IObjectStore _store;
    private readonly string _container;
    private readonly string _key;

    private BlobSessionStore(IObjectStore store, string container, string key) : base()
    {
        _store = store;
        _container = container;
        _key = key;
    }

    public static async Task<BlobSessionStore> OpenAsync(IObjectStore store, string userId, CancellationToken ct = default)
    {
        var s = new BlobSessionStore(store, "telegram", userId);
        if (await store.ExistsAsync("telegram", userId, ct))
        {
            await using var existing = await store.GetAsync("telegram", userId, ct);
            await existing.CopyToAsync(s, ct);
            s.Position = 0;
        }
        return s;
    }

    public override void Flush()
    {
        if (Length == 0) return;
        var current = Position;
        Position = 0;
        // Persist synchronously — WTelegramClient calls Flush on its own cadence.
        _store.PutAsync(_container, _key, new MemoryStream(ToArray()), "application/octet-stream")
            .GetAwaiter().GetResult();
        Position = current;
    }
}
