using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Twitch.Net.Shared.Credential;

/**
 * Recommendation is to only create ONE instance of this to use through the whole application.
 * This is a basic implementation for client credential, if you gonna use the access token of a user
 * it is highly recommended to implement your own solution of "ITokenResolver".
 */
public class ClientCredentialTokenResolver : ITokenResolver
{
    private const string TwitchApiAuthEndpoint = "https://id.twitch.tv/oauth2/token";

    private string _token = string.Empty;
    private string _tokenType = string.Empty;
    private DateTime _tokenExpiration = DateTime.Now;
    private readonly SemaphoreSlim _semaphoreSlim = new(1,1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredentialConfiguration _config;

    public ClientCredentialTokenResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<TokenCredentialConfiguration> config
        )
    {
        _httpClientFactory = httpClientFactory;
        _config = config.Value;
    }
        
    public bool IsTokenExpired() => DateTime.Now >_tokenExpiration;

    public async Task<string> GetToken()
    {
        if (!IsTokenExpired()) return _token;
            
        await _semaphoreSlim.WaitAsync();
        try
        {
            var response = await Authenticate();
            if (response != null)
            {
                _tokenExpiration = DateTime.Now.AddSeconds(response.ExpiresIn);
                _token = response.AccessToken;
                _tokenType = response.TokenType;
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }

        return _token;
    }

    /**
     * Basic implementation will be making "bearer" into "Bearer" because most api calls will require this
     * If you decide to make your own implementation, it is up to you how to deal with this part
     */
    public async Task<string> GetTokenType()
    {
        await GetToken(); // this is to make sure it is up to date
        return !string.IsNullOrEmpty(_tokenType)
            ? char.ToUpper(_tokenType[0]) + _tokenType.Substring(1) // why? Because "bearer" is not valid but "Bearer" is.
            : _tokenType;
    }

    /**
     * This is to get client credentials, it is similar if you need to get another type of token but
     * you may need to pass more stuff & the scopes you are in need of
     */
    private async Task<TwitchClientCredentialAuthResponse> Authenticate()
    {
        if (!IsTokenExpired()) // already updated (semaphore check)
            return null;
            
        using var client = _httpClientFactory.CreateClient();
        var @params = new Dictionary<string, string>
        {
            { "client_id", _config.ClientId },
            { "client_secret", _config.ClientSecret },
            { "grant_type", "client_credentials" },
            { "scopes", "" }
        };
        var content = new FormUrlEncodedContent(@params);

        var response = await client.PostAsync(TwitchApiAuthEndpoint, content);
        if (response.StatusCode == HttpStatusCode.OK)
            return await response.Content.ReadFromJsonAsync<TwitchClientCredentialAuthResponse>();

        return null; // something went wrong
    }
}