using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "1243456",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "title": "Aren’t shoes just really hard socks?",
 * "winning_outcome_id": "12345",
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
 *                  "channel_points_won": null, // null if result is refund or loss
 *                  "channel_points_used": 500
 *              }
 *          ]
 *      }
 * ],
 * "status": "resolved", // valid values: resolved, canceled
 * "started_at": "2020-07-15T17:16:03.17106713Z",
 * "ended_at": "2020-07-15T17:21:03.17106713Z"
 */
public class ChannelPredictEndNotificationEvent : ChannelPredictBaseModel<PredictProgressOutcome>
{
    [JsonPropertyName("winning_outcome_id")]
    public string WinningOutcomingId { get; init; } = string.Empty;
    public PredictProgressOutcome WinnerOutcome => Outcomes.First(x => x.Id == WinningOutcomingId);
    public PredictProgressOutcome LoserOutcome => Outcomes.First(x => x.Id != WinningOutcomingId);
        
    [JsonPropertyName("status")]
    public string StatusString { get; init; } = string.Empty;
    public ChannelPredictStatus PredictStatus => StatusString.ToChannelPredictStatus();
        
    [JsonPropertyName("ended_at")]
    public DateTime EndedAt { get; init; }
}

public enum ChannelPredictStatus
{
    Unknown,
    Resolved,
    Canceled
}

public static class ChannelPredictStatusExtension
{
    // To lower in-case the event sub ever change..
    public static ChannelPredictStatus ToChannelPredictStatus(this string value) => value.ToLower() switch
    {
        "resolved" => ChannelPredictStatus.Resolved,
        "canceled" => ChannelPredictStatus.Canceled,
        _ => ChannelPredictStatus.Unknown
    };
}