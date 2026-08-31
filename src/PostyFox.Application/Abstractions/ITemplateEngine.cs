using PostyFox.Application.Connectors;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Abstractions;

/// <summary>Input for rendering a post for a specific platform.</summary>
public sealed record RenderRequest(
    string Platform,
    string? Title,
    string MarkdownBody,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaRef> Media,
    ContentRating? Rating = null,
    /// <summary>Whether the author chose to include tags for this target (default on).</summary>
    bool IncludeTags = true,
    /// <summary>
    /// Whether the platform has a native tags field (see
    /// <see cref="Connectors.ConnectorDescriptor.SupportsTags"/>). False means tags are woven into
    /// the body instead, at an author-placed <c>{tags}</c> token or appended to the end.
    /// </summary>
    bool SupportsTags = true,
    /// <summary>Static character-limit hint used to trim inline hashtags so the body stays under it.</summary>
    int? MaxContentLength = null,
    /// <summary>
    /// Resolved text-template values for this specific delivery target, keyed by template name
    /// (case-insensitive) — already picked per this target's connector (its override, or the
    /// template's default) by the caller. Substituted into <c>{{tt:name}}</c> tokens in the title and
    /// body before anything else runs, so unlike <see cref="Variables"/> (one value shared by every
    /// target on the post) this can — and typically does — differ per target. An unrecognized name
    /// resolves to an empty string, same as an unknown <c>{variable}</c>.
    /// </summary>
    IReadOnlyDictionary<string, string>? TextTemplateValues = null);

/// <summary>
/// Renders template bodies: variable substitution, conditionals, and per-platform formatting.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>Substitute variables and evaluate conditionals in a raw template body.</summary>
    string Substitute(string body, IReadOnlyDictionary<string, string> variables);

    /// <summary>Produce a platform-appropriate rendered post.</summary>
    RenderedPost Render(RenderRequest request);
}
