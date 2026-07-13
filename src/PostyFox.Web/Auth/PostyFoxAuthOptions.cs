namespace PostyFox.Web.Auth;

public sealed class PostyFoxAuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Local/test-only identity: authenticates every request as <see cref="DevUserId"/> without any
    /// credential. This is NOT a deployment parameter — no shipped configuration enables it; it is set
    /// in-memory by the test host only. Real deployments authenticate via the OIDC bearer token
    /// (<see cref="Oidc"/>) or an API key.
    /// </summary>
    public bool DevMode { get; set; }
    public string DevUserId { get; set; } = "dev-user";

    /// <summary>
    /// Identity header injected by oauth2-proxy after OIDC validation. Only consulted in DevMode;
    /// outside DevMode the APIs validate the OIDC bearer token themselves (see <see cref="Oidc"/>).
    /// </summary>
    public string UserHeader { get; set; } = "X-Auth-Request-User";
    public string? EmailHeader { get; set; } = "X-Auth-Request-Email";

    /// <summary>In-app OIDC bearer-token validation (defence-in-depth; not dependent on the edge).</summary>
    public OidcOptions Oidc { get; set; } = new();

    public sealed class OidcOptions
    {
        /// <summary>When true, an `Authorization: Bearer` token is validated against the OIDC keys.</summary>
        public bool Enabled { get; set; }
        /// <summary>Expected token issuer (the `iss` claim), e.g. the browser-facing Keycloak URL.</summary>
        public string? Issuer { get; set; }
        /// <summary>JWKS endpoint to fetch signing keys from (a back-channel-reachable URL).</summary>
        public string? JwksUrl { get; set; }
        /// <summary>Optional expected audience; when empty, audience is not validated.</summary>
        public string? Audience { get; set; }
    }
}
