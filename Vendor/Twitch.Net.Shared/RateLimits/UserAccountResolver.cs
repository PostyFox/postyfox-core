using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Twitch.Net.Shared.Configurations;
using Twitch.Net.Shared.Credential;

namespace Twitch.Net.Shared.RateLimits;

/**
 * This is a auto resolver based on kraken - WHICH might and can be removed in the future
 * If the auto resolver for the "user account" stops working you'll have to manually set the information.
 * The reason why this is outside of "API" library too is because of the use-cases it can be used for
 */
public class UserAccountResolver : IUserAccountStatusResolver
{
    private const string KrakenBaseUrl = "https://api.twitch.tv/kraken/";
    private const string HelixBaseUrl = "https://api.twitch.tv/helix/";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AccountCredentialConfiguration _accountConfig;
    private readonly TokenCredentialConfiguration _tokenConfig;
    private readonly ITokenResolver _clientTokenResolver;

    public UserAccountResolver(
        IHttpClientFactory httpClientFactory,
        ITokenResolver tokenResolver,
        IOptions<AccountCredentialConfiguration> accountConfig,
        IOptions<TokenCredentialConfiguration> tokenConfig
        )
    {
        _httpClientFactory = httpClientFactory;
        _accountConfig = accountConfig.Value;
        _tokenConfig = tokenConfig.Value;
        _clientTokenResolver = tokenResolver;
    }

    public async Task<UserAccountStatus> ResolveUserAccountStatusAsync() =>
        await TryResolveUserAccountStatusOrDefaultAsync();

    private async Task<UserAccountStatus> TryResolveUserAccountStatusOrDefaultAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_accountConfig.Username) ||
                string.IsNullOrEmpty(_tokenConfig.ClientId) ||
                string.IsNullOrEmpty(_tokenConfig.ClientSecret))
                return new UserAccountStatus();

            var token = await _clientTokenResolver.GetToken();
            var type = await _clientTokenResolver.GetTokenType();
            if (string.IsNullOrEmpty(token))
                return new UserAccountStatus();

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"{type} {token}");
            client.DefaultRequestHeaders.Add("Client-Id", $"{_tokenConfig.ClientId}");

            // resolve the actual user Id
            var currentResponse = await client.GetAsync($"{HelixBaseUrl}users?login={_accountConfig.Username}");
            if (currentResponse.StatusCode != HttpStatusCode.OK)
                return new UserAccountStatus();

            var currentResult = await currentResponse.Content.ReadFromJsonAsync<UsersResponse>();

            if (currentResult == null || !currentResult.Users.Any() ||
                string.IsNullOrEmpty(currentResult.Users[0].UserId))
                return new UserAccountStatus();

            // resolve the user account info (undocumented endpoint as of why it has a fallback for status code != OK)
            // anyone could make an override for this class themself if they need a "verified bot" user account status
            // even if the endpoint is gone, to make the lib use "increased" limit rates.
            var userAccountResponse = await client.GetAsync(
                $"{KrakenBaseUrl}users/{currentResult.Users[0].UserId}/chat?api_version=5&client_id={_tokenConfig.ClientId}");
            if (userAccountResponse.StatusCode != HttpStatusCode.OK)
                return new UserAccountStatus();

            return await userAccountResponse.Content.ReadFromJsonAsync<UserAccountStatus>();
        }
        catch
        {
            /* empty block */
        }
        return new UserAccountStatus();
    }

    private class UsersResponse
    {
        [JsonPropertyName("data")]
        public CurrentUser[] Users { get; init; } = Array.Empty<CurrentUser>();
    }

    private class CurrentUser
    {
        [JsonPropertyName("id")]
        public string UserId { get; init; } = string.Empty;
    }
}