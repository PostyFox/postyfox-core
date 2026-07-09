using System.Security.Claims;

namespace PostyFox.Web.Auth;

public static class UserContext
{
    /// <summary>The authenticated user id (OIDC subject or API-key owner), or null.</summary>
    public static string? UserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier);
}
