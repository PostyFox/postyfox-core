using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostyFox.Web.Auth;

namespace PostyFox.Web.Extensions;

public static class WebExtensions
{
    /// <summary>
    /// Registers authentication. Requests are routed by credential:
    ///   - <c>X-API-Key</c> header       → hashed API-key scheme (external/machine callers)
    ///   - <c>Authorization: Bearer</c>  → OIDC JWT validated in-app (JWKS) when OIDC is enabled
    ///   - otherwise                     → DevMode-only local identity (no header trust in prod)
    /// The OIDC edge (oauth2-proxy) should be the only public route and forward the bearer token.
    /// </summary>
    public static IServiceCollection AddPostyFoxAuth(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PostyFoxAuthOptions>(config.GetSection(PostyFoxAuthOptions.SectionName));

        services.AddHttpClient("jwks");
        services.AddSingleton<IJwksProvider, CachedJwksProvider>();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();

        services.AddAuthentication(AuthConstants.PolicyScheme)
            .AddPolicyScheme(AuthConstants.PolicyScheme, "PostyFox", o =>
            {
                o.ForwardDefaultSelector = ctx =>
                {
                    if (ctx.Request.Headers.ContainsKey(AuthConstants.ApiKeyHeader))
                        return AuthConstants.ApiKey;

                    var oidc = ctx.RequestServices.GetRequiredService<IOptions<PostyFoxAuthOptions>>().Value.Oidc;
                    if (oidc.Enabled &&
                        ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return AuthConstants.Jwt;

                    return AuthConstants.Header;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(AuthConstants.Header, null)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthConstants.ApiKey, null)
            .AddJwtBearer(AuthConstants.Jwt);

        services.AddAuthorization();
        return services;
    }
}
