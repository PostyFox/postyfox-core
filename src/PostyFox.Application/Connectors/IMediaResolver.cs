namespace PostyFox.Application.Connectors;

/// <summary>
/// The single fetch-and-normalize seam every connector uses to turn media references into
/// upload-ready bytes. It enforces the attachment cap, fetches each item from the object store, and
/// runs it through the <see cref="IMediaProcessor"/> against the connector's <see cref="MediaSpec"/>.
/// Routing all media through here means no connector can accidentally upload un-normalized bytes.
/// </summary>
public interface IMediaResolver
{
    Task<IReadOnlyList<MediaContent>> ResolveAsync(
        IReadOnlyList<MediaRef> refs, MediaSpec spec, CancellationToken ct = default);
}
