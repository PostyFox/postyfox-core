using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
* "id": "1243456",
* "broadcaster_user_id": "1337",
* "broadcaster_user_login": "cool_user",
* "broadcaster_user_name": "Cool_User",
* "title": "Aren’t shoes just really hard socks?",
* "choices": [
*      {"id": "123", "title": "Yeah!", "bits_votes": 5, "channel_points_votes": 7, "votes": 12},
*      {"id": "124", "title": "No!", "bits_votes": 10, "channel_points_votes": 4, "votes": 14},
*      {"id": "125", "title": "Maybe!", "bits_votes": 0, "channel_points_votes": 7, "votes": 7}
* ],
* "bits_voting": {
*      "is_enabled": true,
*      "amount_per_vote": 10
* },
* "channel_points_voting": {
*      "is_enabled": true,
*      "amount_per_vote": 10
* },
* "started_at": "2020-07-15T17:16:03.17106713Z",
* "ends_at": "2020-07-15T17:16:08.17106713Z"
*/
public class ChannelPollProgressNotificationEvent : ChannelPollBaseModel<PollProgressChoiceModel>
{
    // Empty - Nothing expect base in this event
}

/**
* "id": "123",
* "title": "Yeah!",
* "bits_votes": 5,
* "channel_points_votes": 7,
* "votes": 12
*/
public class PollProgressChoiceModel : PollBeginChoiceModel
{
    [JsonPropertyName("bits_votes")]
    public string BitVotes { get; init; } = string.Empty;
        
    [JsonPropertyName("channel_points_votes")]
    public string PointsVotes { get; init; } = string.Empty;
        
    [JsonPropertyName("votes")]
    public string Votes { get; init; } = string.Empty;
}