using PostyFox.Domain.Enums;

namespace PostyFox.Domain.Entities;

/// <summary>
/// A user's configured instance of a <see cref="ServiceDefinition"/>. Non-secret config
/// lives in <see cref="ConfigJson"/>; secret config is stored in the secret store under
/// the key {Id}:{UserId}.
/// </summary>
public class UserConnector
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ServiceDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Default "include tags" value for a new post target on this connector (see
    /// <see cref="PostTarget.IncludeTags"/>). Applied whenever the author doesn't override the choice
    /// for this connector on a given post; still overridable per post unless the platform declares
    /// <see cref="Connectors.ConnectorDescriptor.RequiresTags"/>.
    /// </summary>
    public bool DefaultIncludeTags { get; set; } = true;
    /// <summary>
    /// Default content rating to pre-fill for a new post target on this connector, on platforms that
    /// can represent one (see <see cref="Connectors.ConnectorDescriptor.SupportsRating"/>). Purely a
    /// compose-form convenience: intake stores whatever rating the post was actually submitted with
    /// (see <see cref="PostTarget.Rating"/>) and does not fall back to this itself. Null means no
    /// default is configured, either because the platform has no rating concept or the user hasn't
    /// set one.
    /// </summary>
    public ContentRating? DefaultRating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }
}
