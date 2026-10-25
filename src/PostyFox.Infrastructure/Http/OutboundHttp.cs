namespace PostyFox.Infrastructure.Http;

/// <summary>Defaults for HttpClients that call third-party services.</summary>
public static class OutboundHttp
{
    /// <summary>Self-identifying User-Agent; matches DEFAULT_USER_AGENT in connectors-node.</summary>
    public const string UserAgent = "Mozilla/5.0 (compatible; PostyFox/1.0; +http://docs.postyfox.com)";

    public static void UseDefaultUserAgent(HttpClient client) =>
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
}
