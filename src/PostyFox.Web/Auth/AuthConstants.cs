namespace PostyFox.Web.Auth;

public static class AuthConstants
{
    public const string PolicyScheme = "PostyFox";
    public const string Header = "Header";
    public const string ApiKey = "ApiKey";
    public const string Jwt = "Jwt";
    public const string ApiKeyHeader = "X-API-Key";
    public const string AdminPolicy = "PostyFoxAdmin";

    /// <summary>
    /// Carries the owner UserId the caller wants to act as (issue #409: account delegation). Honoured
    /// only when an <c>AccountMember</c> row grants the authenticated caller access to that owner;
    /// see <see cref="ActAsClaimsTransformation"/>.
    /// </summary>
    public const string ActAsHeader = "X-Act-As";

    /// <summary>Claim added by <see cref="ActAsClaimsTransformation"/> carrying the real signed-in UserId when acting as someone else.</summary>
    public const string ActingAsClaim = "postyfox:acting_as";
}
