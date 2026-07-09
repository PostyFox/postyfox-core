namespace PostyFox.Web.Auth;

public sealed class PostyFoxAuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When true, requests are authenticated as <see cref="DevUserId"/> without headers.</summary>
    public bool DevMode { get; set; }
    public string DevUserId { get; set; } = "dev-user";

    /// <summary>Identity header injected by oauth2-proxy after OIDC validation.</summary>
    public string UserHeader { get; set; } = "X-Auth-Request-User";
    public string? EmailHeader { get; set; } = "X-Auth-Request-Email";
}
