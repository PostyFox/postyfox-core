using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Web.Auth;

namespace PostyFox.Web.Extensions;

public static class WebExtensions
{
    /// <summary>
    /// Registers dual authentication: oauth2-proxy header identity by default, or API key when
    /// the X-API-Key header is present.
    /// </summary>
    public static IServiceCollection AddPostyFoxAuth(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PostyFoxAuthOptions>(config.GetSection(PostyFoxAuthOptions.SectionName));

        services.AddAuthentication(AuthConstants.PolicyScheme)
            .AddPolicyScheme(AuthConstants.PolicyScheme, "PostyFox", o =>
            {
                o.ForwardDefaultSelector = ctx =>
                    ctx.Request.Headers.ContainsKey(AuthConstants.ApiKeyHeader)
                        ? AuthConstants.ApiKey
                        : AuthConstants.Header;
            })
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(AuthConstants.Header, null)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthConstants.ApiKey, null);

        services.AddAuthorization();
        return services;
    }
}
