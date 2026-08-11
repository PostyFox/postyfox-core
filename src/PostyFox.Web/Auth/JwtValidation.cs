using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace PostyFox.Web.Auth;

/// <summary>Fetches and caches OIDC signing keys (JWKS) from a back-channel-reachable URL.</summary>
public interface IJwksProvider
{
    IReadOnlyCollection<SecurityKey> GetSigningKeys(string jwksUrl);
}

public sealed class CachedJwksProvider(IHttpClientFactory httpFactory, ILogger<CachedJwksProvider> logger) : IJwksProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (DateTimeOffset fetched, IReadOnlyCollection<SecurityKey> keys)> _cache = new();

    public IReadOnlyCollection<SecurityKey> GetSigningKeys(string jwksUrl)
    {
        if (_cache.TryGetValue(jwksUrl, out var cached) && DateTimeOffset.UtcNow - cached.fetched < Ttl)
            return cached.keys;

        try
        {
            var client = httpFactory.CreateClient("jwks");
            var json = client.GetStringAsync(jwksUrl).GetAwaiter().GetResult();
            IReadOnlyCollection<SecurityKey> keys = JsonWebKeySet.Create(json).GetSigningKeys().ToList();
            _cache[jwksUrl] = (DateTimeOffset.UtcNow, keys);
            return keys;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch JWKS from {Url}", jwksUrl);
            // Serve stale keys if we have them; otherwise none (token validation will fail closed).
            return cached.keys ?? [];
        }
    }
}

/// <summary>
/// Configures the JwtBearer scheme from <see cref="PostyFoxAuthOptions.Oidc"/> at options-resolution
/// time (so test/host-injected config is honoured). Validates issuer + lifetime (+ optional audience)
/// and resolves signing keys from the configured JWKS URL.
/// </summary>
public sealed class ConfigureJwtBearer(IOptions<PostyFoxAuthOptions> auth, IJwksProvider jwks)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != AuthConstants.Jwt) return;
        var o = auth.Value.Oidc;

        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = true; // maps `sub` → ClaimTypes.NameIdentifier
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = o.Issuer,
            ValidateAudience = !string.IsNullOrEmpty(o.Audience),
            ValidAudience = o.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeyResolver = (_, _, _, _) =>
                string.IsNullOrEmpty(o.JwksUrl) ? [] : jwks.GetSigningKeys(o.JwksUrl),
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                    AddKeycloakRealmRoles(identity);
                return Task.CompletedTask;
            }
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    private static void AddKeycloakRealmRoles(ClaimsIdentity identity)
    {
        var realmAccess = identity.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess)) return;

        try
        {
            using var json = JsonDocument.Parse(realmAccess);
            if (!json.RootElement.TryGetProperty("roles", out var roles) ||
                roles.ValueKind != JsonValueKind.Array)
                return;

            var existing = identity.FindAll(identity.RoleClaimType)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var role in roles.EnumerateArray())
            {
                var value = role.GetString();
                if (!string.IsNullOrWhiteSpace(value) && existing.Add(value))
                    identity.AddClaim(new Claim(identity.RoleClaimType, value));
            }
        }
        catch (JsonException)
        {
            // A malformed claim grants no roles; token authentication itself remains valid.
        }
    }
}
