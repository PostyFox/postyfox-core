using System.Text.Json.Serialization;

namespace Twitch.Net.Api.Apis.Helix.Models;

public class HelixChannelsResponse
{
    [JsonPropertyName("data")]
    public List<HelixChannelResponse> Channels { get; init; }
        
    public int Requests { get; init; }
    public int Successfully { get; init; }
    public bool AllSuccess() => Requests == Successfully && Requests > 0;
}