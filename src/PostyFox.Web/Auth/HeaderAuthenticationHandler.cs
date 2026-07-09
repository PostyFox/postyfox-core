using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PostyFox.Web.Auth;

/// <summary>
/// Trusts the identity header injected by the oauth2-proxy ingress (which performs the OIDC
/// exchange against Keycloak). In DevMode, authenticates as a fixed dev user.
/// </summary>
public sealed class HeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<PostyFoxAuthOptions> authOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private readonly PostyFoxAuthOptions _auth = authOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? userId;
        if (_auth.DevMode)
        {
            userId = _auth.DevUserId;
        }
        else
        {
            userId = Request.Headers.TryGetValue(_auth.UserHeader, out var v) ? v.ToString() : null;
            if (string.IsNullOrWhiteSpace(userId))
                return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId!) };
        if (_auth.EmailHeader is { } eh && Request.Headers.TryGetValue(eh, out var email) && !string.IsNullOrEmpty(email))
            claims.Add(new Claim(ClaimTypes.Email, email!));

        var identity = new ClaimsIdentity(claims, AuthConstants.Header);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthConstants.Header);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
