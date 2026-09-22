using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;

namespace PostyFox.Web.Auth;

/// <summary>
/// Resolves account delegation (issue #409): when the request carries <see cref="AuthConstants.ActAsHeader"/>
/// naming an owner UserId, and an <c>AccountMember</c> row grants the authenticated caller access to
/// that owner, the caller's <see cref="ClaimTypes.NameIdentifier"/> is swapped to the owner's id for
/// the rest of the request. Every existing endpoint reads the current user via
/// <see cref="UserContext.UserId"/>, so this is the only place delegation needs to be resolved — no
/// endpoint has to know about it.
///
/// A missing membership (stale header, revoked access, tampering) is not an error: the header is
/// simply ignored and the request proceeds as the caller's own identity, which is always safe since
/// nothing is granted beyond what the membership check itself allows.
/// </summary>
public sealed class ActAsClaimsTransformation(IHttpContextAccessor httpContextAccessor, IAppDbContext db) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true }) return principal;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null) return principal;
        if (!httpContext.Request.Headers.TryGetValue(AuthConstants.ActAsHeader, out var header)) return principal;

        var ownerUserId = header.ToString().Trim();
        var callerUserId = principal.UserId();
        if (string.IsNullOrEmpty(ownerUserId) || string.IsNullOrEmpty(callerUserId) || ownerUserId == callerUserId)
            return principal;

        var hasAccess = await db.AccountMembers.AnyAsync(m => m.OwnerUserId == ownerUserId && m.MemberUserId == callerUserId);
        if (!hasAccess) return principal;

        var identity = new ClaimsIdentity(principal.Identity!.AuthenticationType);
        foreach (var claim in principal.Claims.Where(c => c.Type != ClaimTypes.NameIdentifier))
            identity.AddClaim(claim);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, ownerUserId));
        identity.AddClaim(new Claim(AuthConstants.ActingAsClaim, callerUserId));

        return new ClaimsPrincipal(identity);
    }
}
