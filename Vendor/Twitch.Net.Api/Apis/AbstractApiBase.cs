using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Optional;
using RateLimiter;
using Twitch.Net.Api.Configurations;
using Twitch.Net.Shared.Credential;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.Api.Apis;

public abstract class AbstractApiBase : IApiBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AbstractApiBase> _logger;
    private readonly TimeLimiter _rateLimiter;

    protected AbstractApiBase(
        IHttpClientFactory httpClientFactory, 
        TimeLimiter rateLimiter, 
        ILogger<AbstractApiBase> logger = null
        )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    protected abstract ITokenResolver TokenResolver { get; }
    public abstract string BaseUrl { get; }
    public abstract string ClientIdHeaderKey { get; }
    public abstract IReadOnlyDictionary<string, string> ExtraHeaders { get; }
    public abstract ApiCredentialConfig Config { get; }

    public async Task<Option<T>> GetAsync<T>(
        string segment, 
        IReadOnlyList<KeyValuePair<string, string>> parameters = null, 
        string token = null
        )
    {
        try
        {
            var authorization = !string.IsNullOrEmpty(token)
                ? token
                : await ResolveToken();

            if (string.IsNullOrEmpty(Config.ClientId) || string.IsNullOrEmpty(authorization))
                return Option.None<T>();

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", authorization);
            client.DefaultRequestHeaders.Add(ClientIdHeaderKey, $"{Config.ClientId}");
            ExtraHeaders.ForEach(extra =>
                client.DefaultRequestHeaders.Add(extra.Key, extra.Value));

            var query = parameters == null || parameters.Count == 0
                ? string.Empty
                : parameters.Select(set => $"{set.Key}={set.Value}").Join('&');

            var response = await _rateLimiter.Enqueue(
                async () => await client.GetAsync(
                    $"{BaseUrl}{segment}{(!string.IsNullOrEmpty(query) ? $"?{query}" : string.Empty)}"));
            if (response.StatusCode != HttpStatusCode.OK)
                return Option.None<T>();
            var currentResult = await response.Content.ReadFromJsonAsync<T>();
            if (currentResult != null)
                return currentResult.Some();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Get against twitch api failed");
        }

        return Option.None<T>();
    }
        
    // TODO Add support for DELETE / PUT / POST

    private async Task<string> ResolveToken()
    {
        var token = await TokenResolver.GetToken();
        var type = await TokenResolver.GetTokenType();
        return $"{type} {token}";
    }
}