namespace PostyFox.Domain.Entities;

/// <summary>
/// A platform user. The Id is the stable OIDC subject provided by the auth layer
/// (oauth2-proxy header / bearer token subject).
/// </summary>
public class User
{
    public string Id { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
