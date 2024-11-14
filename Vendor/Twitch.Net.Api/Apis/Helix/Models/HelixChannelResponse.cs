using System.Text.Json.Serialization;

namespace Twitch.Net.Api.Apis.Helix.Models;

public class HelixChannelResponse
{
    [JsonPropertyName("broadcaster_id")]
    public string BroadcasterId { get; init; }
    [JsonPropertyName("broadcaster_login")]
    public string BroadcasterLogin { get; init; }
    [JsonPropertyName("broadcaster_name")]
    public string BroadcasterName { get; init; }
    [JsonPropertyName("broadcaster_language")]
    public string BroadcasterLanguage { get; init; }
    [JsonPropertyName("game_id")]
    public string GameId { get; init; }
    [JsonPropertyName("game_name")]
    public string GameName { get; init; }
    [JsonPropertyName("title")]
    public string Title { get; init; }
    [JsonPropertyName("delay")]
    public int Delay { get; init; }
}