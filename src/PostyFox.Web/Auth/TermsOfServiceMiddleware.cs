using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PostyFox.Application.Services;

namespace PostyFox.Web.Auth;

/// <summary>Endpoint metadata exempting an authenticated endpoint from <see cref="TermsOfServiceMiddleware"/>.</summary>
public sealed class AllowWithoutTermsAcceptanceMetadata;

/// <summary>
/// Issue #417: while terms of service are in force, every authenticated request (OIDC or API key) is
/// refused with 403 until the signed-in person has accepted the current version. When acting as
/// another account, that account's owner must also have accepted. Anonymous endpoints and those marked
/// <see cref="TermsOfServiceExtensions.AllowWithoutTermsAcceptance{TBuilder}"/> are exempt.
/// </summary>
public sealed class TermsOfServiceMiddleware(RequestDelegate next)
{
    public const string NotAcceptedCode = "terms_not_accepted";
    public const string OwnerNotAcceptedCode = "owner_terms_not_accepted";

    public async Task InvokeAsync(HttpContext context, TermsOfServiceService terms)
    {
        var endpoint = context.GetEndpoint();
        if (context.User.Identity is not { IsAuthenticated: true } ||
            endpoint is null ||
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            endpoint.Metadata.GetMetadata<AllowWithoutTermsAcceptanceMetadata>() is not null)
        {
            await next(context);
            return;
        }

        var ct = context.RequestAborted;
        if (await terms.GetCurrentVersionAsync(ct) is not { } version)
        {
            await next(context);
            return;
        }

        var callerUserId = context.User.CallerUserId()!;
        var ownerUserId = context.User.UserId()!;
        if (!await terms.HasAcceptedAsync(callerUserId, version, ct))
        {
            await Refuse(context, NotAcceptedCode, "You must accept the current terms of service.", version);
            return;
        }
        if (ownerUserId != callerUserId && !await terms.HasAcceptedAsync(ownerUserId, version, ct))
        {
            await Refuse(context, OwnerNotAcceptedCode, "The owner of this account must accept the current terms of service.", version);
            return;
        }

        await next(context);
    }

    private static Task Refuse(HttpContext context, string code, string detail, int version) =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Terms of service not accepted",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["termsVersion"] = version })
        .ExecuteAsync(context);
}

public static class TermsOfServiceExtensions
{
    public static TBuilder AllowWithoutTermsAcceptance<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new AllowWithoutTermsAcceptanceMetadata());
}
