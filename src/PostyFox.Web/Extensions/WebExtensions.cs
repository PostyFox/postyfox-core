using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
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

        services.AddHttpContextAccessor();
        // Issue #409: resolves "act as" account delegation for every authenticated request, regardless
        // of which scheme authenticated it — see ActAsClaimsTransformation.
        services.AddTransient<IClaimsTransformation, ActAsClaimsTransformation>();

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

        var adminRole = config[$"{PostyFoxAuthOptions.SectionName}:AdminRole"] ?? "postyfox-admin";
        services.AddAuthorization(options =>
            options.AddPolicy(AuthConstants.AdminPolicy, policy =>
                policy.RequireAuthenticatedUser().RequireRole(adminRole)));
        return services;
    }

    /// <summary>Cultures with API message translations (issue #33). The first is the default.</summary>
    public static readonly string[] SupportedCultures = ["en-GB"];

    /// <summary>
    /// Issue #33: API messages follow the client's Accept-Language when it names a supported
    /// culture, otherwise the default. Only the header is honoured (no query string or cookie).
    /// </summary>
    public static IApplicationBuilder UsePostyFoxLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(o =>
        {
            o.SetDefaultCulture(SupportedCultures[0])
                .AddSupportedCultures(SupportedCultures)
                .AddSupportedUICultures(SupportedCultures);
            o.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        });

    /// <summary>Issue #417 terms gate. Runs after authorisation (unauthenticated calls still get 401) and rate limiting.</summary>
    public static IApplicationBuilder UsePostyFoxTermsOfService(this IApplicationBuilder app) =>
        app.UseMiddleware<TermsOfServiceMiddleware>();
}
