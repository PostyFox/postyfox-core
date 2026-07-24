using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Tests.Support;

/// <summary>
/// Test double for <see cref="IMediaResolver"/>. Records the spec it was handed and returns one
/// <see cref="MediaContent"/> per ref (a filename derived from the key, the ref's content type, and
/// placeholder bytes) unless a custom <see cref="Map"/> is supplied. Lets connector tests assert
/// "the connector routed media through the resolver with its declared spec" without real codecs.
/// </summary>
public sealed class FakeMediaResolver : IMediaResolver
{
    public MediaSpec? LastSpec { get; private set; }
    public int LastCount { get; private set; }
    public Func<MediaRef, MediaContent>? Map { get; set; }

    public Task<IReadOnlyList<MediaContent>> ResolveAsync(
        IReadOnlyList<MediaRef> refs, MediaSpec spec, CancellationToken ct = default)
    {
        LastSpec = spec;
        LastCount = refs.Count;
        var list = refs.Select(r => Map?.Invoke(r) ?? Default(r)).ToList();
        return Task.FromResult<IReadOnlyList<MediaContent>>(list);
    }

    private static MediaContent Default(MediaRef r)
    {
        var name = r.Key.Contains('/') ? r.Key[(r.Key.LastIndexOf('/') + 1)..] : r.Key;
        return new MediaContent(name, r.ContentType, [1, 2, 3], r.Alt);
    }
}
