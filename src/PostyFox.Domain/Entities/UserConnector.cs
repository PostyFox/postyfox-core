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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }
}
