using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

/**
* "is_enabled": true,
* "amount_per_vote": 10
*/
public class PointsVotingModel
{ 
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }

    [JsonPropertyName("amount_per_vote")]
    public int AmountPerVote { get; init; }
}