using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PostyFox.Application;
using PostyFox.Application.Connectors;

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
    IOptions<NodeConnectorsOptions> options) : IConnector, IOAuthConnector
{
    private readonly NodeConnectorsOptions _opts = options.Value;

    public ConnectorDescriptor Describe() => descriptor;

    public async Task<OAuthStart?> StartAuthorizationAsync(string callbackUrl, CancellationToken ct = default)
    {
        var res = await PostAsync("oauth/request-token", new { callbackUrl }, ct);
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
        var res = await PostAsync("oauth/access-token", new { requestToken, requestTokenSecret, verifier }, ct);
        if (res is null) return null;
        return res.Value.TryGetProperty("secretJson", out var s) ? s.GetString() : null;
    }

    public async Task<AuthState> IsAuthenticatedAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var res = await PostAsync($"is-authenticated", Ctx(context), ct);
        if (res is null) return new AuthState(false, "connectors-node unavailable");
        var authed = res.Value.TryGetProperty("isAuthenticated", out var a) && a.GetBoolean();
        var detail = res.Value.TryGetProperty("detail", out var d) ? d.GetString() : null;
        return new AuthState(authed, detail);
    }

    public async Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var res = await PostAsync("list-targets", Ctx(context), ct);
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
            context = Ctx(context),
            post = new
            {
                title = post.Title,
                body = post.Body,
                tags = post.Tags,
                // Media is passed by reference; the Node service fetches the bytes from the object store.
                media = post.Media.Select(m => new { container = m.Container, key = m.Key, contentType = m.ContentType, alt = m.Alt })
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

    private static object Ctx(ConnectorContext c) => new
    {
        connectorId = c.ConnectorId,
        userId = c.UserId,
        configJson = c.ConfigJson,
        secretJson = c.SecretJson,
        targetId = c.TargetId
    };

    private async Task<JsonElement?> PostAsync(string op, object body, CancellationToken ct)
    {
        var client = httpFactory.CreateClient(nameof(HttpConnector));
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl.TrimEnd('/')}/connectors/{platform}/{op}")
        {
            Content = JsonContent.Create(body, options: Json.Options)
        };
        if (!string.IsNullOrEmpty(_opts.InternalToken))
            req.Headers.Add("X-Internal-Token", _opts.InternalToken);

        var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }
}
