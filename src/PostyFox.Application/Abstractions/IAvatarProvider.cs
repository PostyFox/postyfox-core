namespace PostyFox.Application.Abstractions;

/// <summary>An avatar image fetched on the user's behalf.</summary>
public sealed record AvatarImage(byte[] Content, string ContentType);

/// <summary>
/// Looks up a profile image for an email address (issue #420, Gravatar). Fetched server-side so the
/// browser never talks to the avatar service directly.
/// </summary>
public interface IAvatarProvider
{
    /// <summary>The avatar for <paramref name="email"/>, or null when none is registered.</summary>
    Task<AvatarImage?> GetAsync(string email, int size, CancellationToken ct = default);
}
