namespace PostyFox.Application.Abstractions;

/// <summary>
/// Cloud-agnostic blob/object storage (backed by any S3-compatible service or Azure Blob).
/// </summary>
public interface IObjectStore
{
    Task PutAsync(string container, string key, Stream content, string contentType, CancellationToken ct = default);
    Task PutTextAsync(string container, string key, string content, string contentType = "text/plain", CancellationToken ct = default);
    Task<Stream> GetAsync(string container, string key, CancellationToken ct = default);
    Task<string> GetTextAsync(string container, string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string container, string key, CancellationToken ct = default);
    Task DeleteAsync(string container, string key, CancellationToken ct = default);

    /// <summary>
    /// Opens an object for streaming without buffering it in memory, or null when it doesn't exist.
    /// The default buffers via <see cref="GetAsync"/>; stores that can stream should override it.
    /// </summary>
    async Task<Stream?> OpenReadAsync(string container, string key, CancellationToken ct = default) =>
        await ExistsAsync(container, key, ct) ? await GetAsync(container, key, ct) : null;
}
