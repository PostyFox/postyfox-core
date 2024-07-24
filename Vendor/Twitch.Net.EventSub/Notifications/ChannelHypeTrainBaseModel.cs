using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "1b0AsbInCHZW2SQFQkCzqN07Ib2",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "total": 137,
 * "top_contributions": [
 *      { "user_id": "123", "user_login": "pogchamp", "user_name": "PogChamp", "type": "bits", "total": 50 },
 *      { "user_id": "456", "user_login": "kappa", "user_name": "Kappa", "type": "subscription", "total": 45 }
 * ],
 * "started_at": "2020-07-15T17:16:03.17106713Z"
 */
public class ChannelHypeTrainBaseModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterIdString { get; init; } = string.Empty;
    public int BroadcasterId => int.Parse(BroadcasterIdString);
        
    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = string.Empty;
        
    [JsonPropertyName("total")]
    public int Total { get; init; }
        
    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; init; }
}

/**
     * "user_id": "123",
     * "user_login": "pogchamp",
     * "user_name": "PogChamp",
     * "type": "bits",
     * "total": 50
     */
public class HypeTrainContributor
{
    [JsonPropertyName("user_id")]
    public string UserIdString { get; init; } = string.Empty;
    public int UserId => int.Parse(UserIdString);
        
    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = string.Empty;
        
    [JsonPropertyName("type")]
    public string TypeString { get; init; } = string.Empty;
    public HypeTrainContributionType Type => TypeString.ToHypeTrainContributionType();
        
    [JsonPropertyName("total")]
    public int Total { get; init; }
}

public enum HypeTrainContributionType
{
    Unknown,
    Bits,
    Subscriptions
}

public static class HypeTrainContributionTypeExtension
{
    public static HypeTrainContributionType ToHypeTrainContributionType(this string value) =>
        value.ToLower() switch
        {
            "bits" => HypeTrainContributionType.Bits,
            "subscription" => HypeTrainContributionType.Subscriptions,
            _ => HypeTrainContributionType.Unknown
        };
}