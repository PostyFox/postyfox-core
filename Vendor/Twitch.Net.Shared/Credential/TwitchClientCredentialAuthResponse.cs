using System.Text.Json.Serialization;

namespace Twitch.Net.Shared.Credential;

public class TwitchClientCredentialAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; } // in seconds

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; }
}