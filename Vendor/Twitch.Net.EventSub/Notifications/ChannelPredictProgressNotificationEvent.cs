using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "1243456",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "title": "Aren’t shoes just really hard socks?",
 * "outcomes": [
 *      {
 *          "id": "1243456",
 *          "title": "Yeah!",
 *          "color": "blue",
 *          "users": 10,
 *          "channel_points": 15000,
 *          "top_predictors": [ // contains up to 10 users
 *              {
 *                  "user_name": "Cool_User",
 *                  "user_login": "cool_user",
 *                  "user_id": "1234",
 *                  "channel_points_won": null,
 *                  "channel_points_used": 500
 *              }
 *          ]
 *      }
 * ],
 * "started_at": "2020-07-15T17:16:03.17106713Z",
 * "locks_at": "2020-07-15T17:21:03.17106713Z"
 */
public class ChannelPredictProgressNotificationEvent : ChannelPredictBaseModel<PredictProgressOutcome>
{
    [JsonPropertyName("locks_at")]
    public DateTime LocksAt { get; init; }
}

/**
 * "id": "1243456",
 * "title": "Yeah!",
 * "color": "blue",
 * "users": 10,
 * "channel_points": 15000,
 * "top_predictors": [ // contains up to 10 users
 *      {
 *          "user_name": "Cool_User",
 *          "user_login": "cool_user",
 *          "user_id": "1234",
 *          "channel_points_won": null,
 *          "channel_points_used": 500
 *      }
 * ]
 */
public class PredictProgressOutcome : PredictBeginOutcome
{
    [JsonPropertyName("users")]
    public int Users { get; init; }
        
    [JsonPropertyName("channel_points")]
    public int ChannelPoints { get; init; }

    [JsonPropertyName("top_predictors")]
    public PredictUserOutcome[] TopPredictors { get; init; } = Array.Empty<PredictUserOutcome>();
}

public class PredictUserOutcome
{
    [JsonPropertyName("user_id")]
    public string UserIdString { get; init; } = string.Empty;
    public int UserId => int.Parse(UserIdString);
        
    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = string.Empty;
        
    [JsonPropertyName("channel_points_won")]
    public int? ChannelPointsWon { get; init; }
        
    [JsonPropertyName("channel_points_used")]
    public int ChannelPointsUsed { get; init; }
}