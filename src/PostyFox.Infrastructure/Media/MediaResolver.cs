using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Connectors;

namespace PostyFox.Infrastructure.Media;

/// <summary>
/// Default <see cref="IMediaResolver"/>: enforces the attachment cap, fetches bytes from the object
/// store (via <see cref="MediaFetcher"/>), and normalizes each item to the platform's
/// <see cref="MediaSpec"/>. This is the one place fetch-and-normalize is wired, so every in-process
/// connector delivers correctly-sized media without duplicating the pipeline.
/// </summary>
public sealed class MediaResolver(IObjectStore objectStore, IMediaProcessor processor) : IMediaResolver
{
    public async Task<IReadOnlyList<MediaContent>> ResolveAsync(
        IReadOnlyList<MediaRef> refs, MediaSpec spec, CancellationToken ct = default)
    {
        if (refs.Count == 0) return [];

        // Enforce the platform's attachment cap before doing any work.
        var capped = spec.MaxAttachments is { } max && refs.Count > max
            ? refs.Take(max).ToList()
            : refs;

        var fetched = await MediaFetcher.FetchAsync(objectStore, capped, ct);
        var result = new List<MediaContent>(fetched.Count);
        foreach (var item in fetched)
            result.Add(await processor.NormalizeAsync(item, spec, ct));
        return result;
    }
}
