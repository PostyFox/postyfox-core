using System.Security.Claims;

namespace PostyFox.Web.Auth;

public static class UserContext
{
    /// <summary>The authenticated user id (OIDC subject or API-key owner), or null.</summary>
    public static string? UserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// The signed-in person's own id. Differs from <see cref="UserId"/> only while acting as another
    /// account (issue #409), where <see cref="UserId"/> is the owner being acted as.
    /// </summary>
    public static string? CallerUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(AuthConstants.ActingAsClaim) ?? principal.UserId();

    /// <summary>The authenticated user's email, from OIDC/DevMode claims, or null.</summary>
    public static string? Email(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email);
}
