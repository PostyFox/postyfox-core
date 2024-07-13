using RateLimiter;
using Twitch.Net.Api.Configurations;
using Twitch.Net.Shared.Credential;

namespace Twitch.Net.Api.Apis.Helix;

public class ApiHelix : AbstractApiBase, IApiHelix
{
    public ApiHelix(
        ITokenResolver tokenResolver, 
        ApiCredentialConfig config,
        IHttpClientFactory httpClientFactory,
        TimeLimiter rateLimiter
        )
        : base(httpClientFactory, rateLimiter)
    {
        TokenResolver = tokenResolver;
        Config = config;

        Users = new Users(this);
    }

    public override string BaseUrl => "https://api.twitch.tv/helix";
    public override string ClientIdHeaderKey => "client-id";
    public override IReadOnlyDictionary<string, string> ExtraHeaders { get; } = new Dictionary<string, string>();
    public override ApiCredentialConfig Config { get; }
    protected override ITokenResolver TokenResolver { get; }
        
    // API:s
    public Users Users { get; }
}