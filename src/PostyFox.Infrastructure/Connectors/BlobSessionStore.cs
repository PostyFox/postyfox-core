using Microsoft.Extensions.Logging;
using PostyFox.Application.Abstractions;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// MTProto session stream persisted to the object store (port of the legacy blob-backed
/// TelegramStore). Loads existing session on open; persists changes back to object storage.
///
/// IMPORTANT: WTelegramClient's <c>Session.Save()</c> persists session state via
/// <c>Position = 0; Write(...); SetLength(...)</c> — it never calls <see cref="Stream.Flush"/>,
/// and <c>Client.Dispose()</c> only disposes the underlying stream (which does not call Flush
/// either). So persistence MUST be triggered from <see cref="Write(byte[], int, int)"/> /
/// <see cref="Write(ReadOnlySpan{byte})"/> (or <see cref="SetLength"/>) rather than from
/// <see cref="Flush"/>, or session state is silently held only in memory and lost when the
/// client is disposed.
/// </summary>
public sealed class BlobSessionStore : MemoryStream
{
    private readonly IObjectStore _store;
    private readonly string _container;
    private readonly string _key;
    private readonly ILogger? _logger;

    private BlobSessionStore(IObjectStore store, string container, string key, ILogger? logger) : base()
    {
        _store = store;
        _container = container;
        _key = key;
        _logger = logger;
    }

    public static async Task<BlobSessionStore> OpenAsync(IObjectStore store, string userId, CancellationToken ct = default, ILogger? logger = null)
    {
        var s = new BlobSessionStore(store, "telegram", userId, logger);
        if (await store.ExistsAsync("telegram", userId, ct))
        {
            await using var existing = await store.GetAsync("telegram", userId, ct);
            await existing.CopyToAsync(s, ct);
            s.Position = 0;
            logger?.LogDebug("Telegram session state loaded from object store for {User} ({Bytes} bytes)", userId, s.Length);
        }
        else
        {
            logger?.LogDebug("No existing Telegram session state found in object store for {User}; starting fresh", userId);
        }
        return s;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        base.Write(buffer, offset, count);
        Persist();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        base.Write(buffer);
        Persist();
    }

    public override void SetLength(long value)
    {
        base.SetLength(value);
        Persist();
    }

    private void Persist()
    {
        if (Length == 0) return;
        var current = Position;
        Position = 0;
        try
        {
            // Persist synchronously — Session.Save() writes/truncates the stream directly and never
            // calls Flush(), so this is the only reliable hook for pushing state to the object store.
            _store.PutAsync(_container, _key, new MemoryStream(ToArray()), "application/octet-stream")
                .GetAwaiter().GetResult();
            _logger?.LogDebug("Persisted Telegram session state to object store {Container}/{Key} ({Bytes} bytes)", _container, _key, Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist Telegram session state to object store {Container}/{Key}", _container, _key);
            throw;
        }
        finally
        {
            Position = current;
        }
    }

    public override void Flush()
    {
        // No-op: WTelegramClient never calls Flush() on the session stream (see class remarks).
        // Persistence happens eagerly in Write/SetLength instead.
    }
}
