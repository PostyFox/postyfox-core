using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PostyFox.Application.Abstractions;

namespace PostyFox.Infrastructure.Avatars;

/// <summary>
/// Fetches avatars from Gravatar by the SHA-256 of the normalised email. <c>d=404</c> makes Gravatar
/// report "no avatar" rather than serve a placeholder, so the UI can keep showing initials. Hits and
/// misses are both cached so a page load doesn't re-query Gravatar.
/// </summary>
public sealed class GravatarAvatarProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<GravatarAvatarProvider> logger) : IAvatarProvider
{
    private const int MaxBytes = 1024 * 1024;
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(1);

    public async Task<AvatarImage?> GetAsync(string email, int size, CancellationToken ct = default)
    {
        var hash = Hash(email);
        var key = $"gravatar:{hash}:{size}";
        if (cache.TryGetValue(key, out AvatarImage? cached)) return cached;

        var image = await FetchAsync(hash, size, ct);
        cache.Set(key, image, CacheFor);
        return image;
    }

    public static string Hash(string email) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));

    private async Task<AvatarImage?> FetchAsync(string hash, int size, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(nameof(GravatarAvatarProvider));
        using var resp = await client.GetAsync(
            $"https://gravatar.com/avatar/{hash}?s={size}&d=404", HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode != System.Net.HttpStatusCode.NotFound)
                logger.LogWarning("Gravatar lookup failed with {Status}", (int)resp.StatusCode);
            return null;
        }

        var contentType = resp.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
        if (resp.Content.Headers.ContentLength > MaxBytes) return null;

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return bytes.Length > MaxBytes ? null : new AvatarImage(bytes, contentType);
    }
}
