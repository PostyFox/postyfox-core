using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Services;

namespace PostyFox.Web.Auth;

/// <summary>
/// Authenticates external/machine callers via a hashed API key presented in the
/// <c>X-API-Key</c> header. Retained for external connectivity per requirements.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    ApiKeyService apiKeys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthConstants.ApiKeyHeader, out var presented) || string.IsNullOrWhiteSpace(presented))
            return AuthenticateResult.NoResult();

        var userId = await apiKeys.ValidateAsync(presented.ToString());
        if (userId is null)
            return AuthenticateResult.Fail("Invalid API key");

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim("auth_method", "apikey")],
            AuthConstants.ApiKey);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthConstants.ApiKey));
    }
}
