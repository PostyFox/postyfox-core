using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>Fetches media bytes from the object store for a set of refs (in-process connectors).</summary>
internal static class MediaFetcher
{
    public static async Task<IReadOnlyList<MediaContent>> FetchAsync(
        IObjectStore store, IReadOnlyList<MediaRef> refs, CancellationToken ct)
    {
        if (refs.Count == 0) return [];
        var list = new List<MediaContent>(refs.Count);
        foreach (var m in refs)
        {
            await using var stream = await store.GetAsync(m.Container, m.Key, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var fileName = m.Key.Contains('/') ? m.Key[(m.Key.LastIndexOf('/') + 1)..] : m.Key;
            list.Add(new MediaContent(fileName, m.ContentType, ms.ToArray(), m.Alt));
        }
        return list;
    }
}
