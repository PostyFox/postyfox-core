using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostyFox.Application;
using PostyFox.Application.Connectors;
using PostyFox.Application.Services;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// Adapter that fulfils <see cref="IConnector"/> for a platform implemented by the external
/// Node connectors service (Bluesky, Tumblr). Forwards operations over HTTP, matching the
/// connectors-node contract, authenticated with the shared internal token.
/// </summary>
public sealed class HttpConnector(
    string platform,
    ConnectorDescriptor descriptor,
    IHttpClientFactory httpFactory,
    IOptions<NodeConnectorsOptions> options,
    ILogger<HttpConnector> logger,
    IServiceScopeFactory? scopeFactory = null) : IConnector, IOAuthConnector, ILimitsConnector, IRepostConnector, IDeleteConnector, IRefreshableConnector
{
    private readonly NodeConnectorsOptions _opts = options.Value;

    public ConnectorDescriptor Describe() => descriptor;

    public async Task<ConnectorLimits?> GetLimitsAsync(ConnectorContext context, CancellationToken ct = default)
    {
        // Platforms whose Node connector has no limits support respond 4xx → PostAsync returns null.
        // That non-2xx is expected here, so don't warn on it (it would be per-post noise for
        // Bluesky/Tumblr); PostAsync still logs the body at Debug for diagnosis.
        var res = await PostAsync("limits", await CtxAsync(context, ct), ct, warnOnFailure: false);
        if (res is null) return null;
        var r = res.Value;
        int? max = r.TryGetProperty("maxContentLength", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : null;
        int? att = r.TryGetProperty("maxMediaAttachments", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : null;
        long? img = r.TryGetProperty("imageSizeLimit", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt64() : null;
        long? vid = r.TryGetProperty("videoSizeLimit", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
        int? imgW = r.TryGetProperty("imageMaxWidth", out var iw) && iw.ValueKind == JsonValueKind.Number ? iw.GetInt32() : null;
        int? imgH = r.TryGetProperty("imageMaxHeight", out var ih) && ih.ValueKind == JsonValueKind.Number ? ih.GetInt32() : null;
        int? vidW = r.TryGetProperty("videoMaxWidth", out var vw) && vw.ValueKind == JsonValueKind.Number ? vw.GetInt32() : null;
        int? vidH = r.TryGetProperty("videoMaxHeight", out var vh) && vh.ValueKind == JsonValueKind.Number ? vh.GetInt32() : null;
        IReadOnlyList<string>? mimes = null;
        if (r.TryGetProperty("supportedMimeTypes", out var t) && t.ValueKind == JsonValueKind.Array)
            mimes = t.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
        return new ConnectorLimits(max, att, mimes, img, vid, imgW, imgH, vidW, vidH);
    }

    public async Task<OAuthStart?> StartAuthorizationAsync(string callbackUrl, string? configJson, CancellationToken ct = default)
    {
        var res = await PostAsync("oauth/request-token", new
        {
            callbackUrl,
            configJson,
            operationalSecretJson = await OperationalSecretJsonAsync(ct)
        }, ct);
        if (res is null) return null;
        var url = res.Value.TryGetProperty("authorizeUrl", out var a) ? a.GetString() : null;
        var token = res.Value.TryGetProperty("requestToken", out var t) ? t.GetString() : null;
        var secret = res.Value.TryGetProperty("requestTokenSecret", out var s) ? s.GetString() : null;
        return url is not null && token is not null && secret is not null
            ? new OAuthStart(url, token, secret)
            : null;
    }

    public async Task<string?> CompleteAuthorizationAsync(string requestToken, string requestTokenSecret, string verifier, CancellationToken ct = default)
    {
        var res = await PostAsync("oauth/access-token", new
        {
            requestToken,
            requestTokenSecret,
            verifier,
            operationalSecretJson = await OperationalSecretJsonAsync(ct)
        }, ct);
        if (res is null) return null;
        return res.Value.TryGetProperty("secretJson", out var s) ? s.GetString() : null;
    }

    public async Task<AuthState> IsAuthenticatedAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var res = await PostAsync("is-authenticated", await CtxAsync(context, ct), ct);
        if (res is null) return new AuthState(false, "connectors-node unavailable");
        var authed = res.Value.TryGetProperty("isAuthenticated", out var a) && a.GetBoolean();
        var detail = res.Value.TryGetProperty("detail", out var d) ? d.GetString() : null;
        return new AuthState(authed, detail);
    }

    public async Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var res = await PostAsync("list-targets", await CtxAsync(context, ct), ct);
        if (res is null || !res.Value.TryGetProperty("targets", out var arr)) return [];
        var list = new List<ConnectorTarget>();
        foreach (var t in arr.EnumerateArray())
            list.Add(new ConnectorTarget(t.GetProperty("id").GetString() ?? "", t.GetProperty("name").GetString() ?? ""));
        return list;
    }

    public async Task<DeliveryResult> DeliverAsync(ConnectorContext context, RenderedPost post, CancellationToken ct = default)
    {
        var payload = new
        {
            context = await CtxAsync(context, ct),
            post = new
            {
                title = post.Title,
                body = post.Body,
                tags = post.Tags,
                rating = post.Rating?.ToString().ToLowerInvariant(),
                // Media is passed by reference; the Node service fetches the bytes from the object store.
                media = post.Media.Select(m => new { container = m.Container, key = m.Key, contentType = m.ContentType, alt = m.Alt, isDefault = m.IsDefault })
            }
        };
        var res = await PostAsync("deliver", payload, ct);
        if (res is null) return DeliveryResult.Fail("connectors-node unavailable");
        var success = res.Value.TryGetProperty("success", out var s) && s.GetBoolean();
        if (success)
            return DeliveryResult.Ok(
                res.Value.TryGetProperty("externalId", out var id) ? id.GetString() : null,
                res.Value.TryGetProperty("externalUrl", out var url) ? url.GetString() : null);
        return DeliveryResult.Fail(res.Value.TryGetProperty("error", out var e) ? e.GetString() ?? "delivery failed" : "delivery failed");
    }

    /// <summary>
    /// Reposts/reblogs/boosts an already-delivered target (issue #323's "repost after X hours"). Only
    /// meaningful when this instance's descriptor declares <see cref="ConnectorDescriptor.SupportsRepost"/>;
    /// wired unconditionally (like <see cref="ILimitsConnector"/>) since the Node service itself is the
    /// authority on whether the platform actually supports it.
    /// </summary>
    public async Task<DeliveryResult> RepostAsync(ConnectorContext context, string externalId, CancellationToken ct = default)
    {
        var payload = new { context = await CtxAsync(context, ct), externalId };
        var res = await PostAsync("repost", payload, ct);
        if (res is null) return DeliveryResult.Fail("connectors-node unavailable");
        var success = res.Value.TryGetProperty("success", out var s) && s.GetBoolean();
        if (success)
            return DeliveryResult.Ok(
                res.Value.TryGetProperty("externalId", out var id) ? id.GetString() : null,
                res.Value.TryGetProperty("externalUrl", out var url) ? url.GetString() : null);
        return DeliveryResult.Fail(res.Value.TryGetProperty("error", out var e) ? e.GetString() ?? "repost failed" : "repost failed");
    }

    /// <summary>
    /// Deletes an already-delivered target from its platform (issue #323's "delete after X hours").
    /// See <see cref="RepostAsync"/> for why this is wired unconditionally.
    /// </summary>
    public async Task<bool> DeleteRemoteAsync(ConnectorContext context, string externalId, CancellationToken ct = default)
    {
        var payload = new { context = await CtxAsync(context, ct), externalId };
        var res = await PostAsync("delete", payload, ct);
        return res is not null && res.Value.TryGetProperty("success", out var s) && s.GetBoolean();
    }

    /// <summary>
    /// Refreshes the connector's stored token ahead of expiry (see <see cref="IRefreshableConnector"/>),
    /// driven by <c>ConnectorTokenRefreshSweeper</c> rather than a user action.
    /// </summary>
    public async Task<string?> RefreshTokenAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var res = await PostAsync("refresh-token", await CtxAsync(context, ct), ct);
        if (res is null) return null;
        return res.Value.TryGetProperty("secretJson", out var s) ? s.GetString() : null;
    }

    private async Task<object> CtxAsync(ConnectorContext c, CancellationToken ct) => new
    {
        connectorId = c.ConnectorId,
        userId = c.UserId,
        configJson = c.ConfigJson,
        secretJson = c.SecretJson,
        targetId = c.TargetId,
        operationalSecretJson = await OperationalSecretJsonAsync(ct)
    };

    private async Task<string?> OperationalSecretJsonAsync(CancellationToken ct)
    {
        if (scopeFactory is null) return null;
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OperationalSecretService>()
            .ConnectorCredentialsJsonAsync(platform, ct);
    }

    private async Task<JsonElement?> PostAsync(string op, object body, CancellationToken ct, bool warnOnFailure = true)
    {
        var client = httpFactory.CreateClient(nameof(HttpConnector));
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl.TrimEnd('/')}/connectors/{platform}/{op}")
        {
            Content = JsonContent.Create(body, options: Json.Options)
        };
        if (!string.IsNullOrEmpty(_opts.InternalToken))
            req.Headers.Add("X-Internal-Token", _opts.InternalToken);

        var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            // Capture the Node service's response body so a failure isn't reduced to a bare status
            // code. Callers collapse a null to a generic message ("connectors-node unavailable"),
            // which otherwise hides the actual cause (auth, unknown platform, a connector throw, …).
            var errorBody = await ReadBodySafeAsync(resp, ct);
            if (warnOnFailure)
                logger.LogWarning(
                    "connectors-node {Operation} for {Platform} returned {StatusCode}: {Body}",
                    op, platform, (int)resp.StatusCode, errorBody);
            else
                logger.LogDebug(
                    "connectors-node {Operation} for {Platform} returned {StatusCode}: {Body}",
                    op, platform, (int)resp.StatusCode, errorBody);
            return null;
        }
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    private static async Task<string> ReadBodySafeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return $"<unreadable response body: {ex.Message}>";
        }
    }
}
