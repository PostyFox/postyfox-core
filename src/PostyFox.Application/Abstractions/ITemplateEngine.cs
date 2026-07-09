using PostyFox.Application.Connectors;

namespace PostyFox.Application.Abstractions;

/// <summary>Input for rendering a post for a specific platform.</summary>
public sealed record RenderRequest(
    string Platform,
    string? Title,
    string MarkdownBody,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaRef> Media);

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
