using Optional;
using Twitch.Net.Api.Configurations;

namespace Twitch.Net.Api.Apis;

public interface IApiBase
{
    string BaseUrl { get; }
    string ClientIdHeaderKey { get; }
    IReadOnlyDictionary<string, string> ExtraHeaders { get; }
    ApiCredentialConfig Config { get; }
        
    /**
         * Pass token if we wanna use a specific access token towards the API endpoint
         */
    Task<Option<T>> GetAsync<T>(
        string segment,
        IReadOnlyList<KeyValuePair<string, string>> parameters = null, 
        string token = null
        );
}