namespace PostyFox.Domain.Entities;

/// <summary>
/// A reusable, named text snippet owned by a user — referenced inline in a post's title/description
/// as <c>{{tt:Name}}</c> and resolved per delivery target (see
/// <see cref="Application.Abstractions.ITemplateEngine"/>). <see cref="DefaultValue"/> is used when
/// the target's connector has no override in <see cref="ConnectorValuesJson"/>; both may be blank,
/// which resolves the token to an empty string rather than leaving it in the post.
/// </summary>
public class TextTemplate
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>The name referenced as <c>{{tt:Name}}</c>. Unique per user (case-insensitive).</summary>
    public string Name { get; set; } = string.Empty;

    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>JSON object mapping a configured connector's id (as a string) to its override value.</summary>
    public string ConnectorValuesJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
