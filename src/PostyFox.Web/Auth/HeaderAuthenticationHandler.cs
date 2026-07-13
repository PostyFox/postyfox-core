using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PostyFox.Web.Auth;

/// <summary>
/// DevMode-only local authentication: authenticates every request as a fixed dev user.
///
/// IMPORTANT: this does NOT trust <c>X-Auth-Request-User</c> outside DevMode. A raw identity header
/// is spoofable by anything that can reach the API directly, so production auth uses the validated
/// OIDC bearer token (<see cref="AuthConstants.Jwt"/>) or an API key instead. The OIDC edge must be
/// the only public route and must forward the bearer token to the upstream APIs.
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
        if (!_auth.DevMode)
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = _auth.DevUserId;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (_auth.EmailHeader is { } eh && Request.Headers.TryGetValue(eh, out var email) && !string.IsNullOrEmpty(email))
            claims.Add(new Claim(ClaimTypes.Email, email!));

        var identity = new ClaimsIdentity(claims, AuthConstants.Header);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthConstants.Header);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
