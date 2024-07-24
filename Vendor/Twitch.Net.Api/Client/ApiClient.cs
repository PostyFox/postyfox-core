using Microsoft.Extensions.Options;
using RateLimiter;
using Twitch.Net.Api.Apis.Helix;
using Twitch.Net.Api.Configurations;
using Twitch.Net.Shared.Credential;

namespace Twitch.Net.Api.Client;

internal class ApiClient : IApiClient
{
    public ApiClient(
        IOptions<ApiCredentialConfig> config, 
        IHttpClientFactory httpClientFactory, 
        ITokenResolver tokenResolver
        )
    {
        var rateLimiter = TimeLimiter.GetFromMaxCountByInterval(800, TimeSpan.FromSeconds(60));
        Helix = new ApiHelix(tokenResolver, config.Value, httpClientFactory, rateLimiter);
    }

    public IApiHelix Helix { get; }
}