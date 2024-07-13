using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
* "id": "1243456",
* "broadcaster_user_id": "1337",
* "broadcaster_user_login": "cool_user",
* "broadcaster_user_name": "Cool_User",
* "title": "Aren’t shoes just really hard socks?",
* "choices": [
*      {"id": "123", "title": "Yeah!"},
*      {"id": "124", "title": "No!"},
*      {"id": "125", "title": "Maybe!"}
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
public class ChannelPollBeginNotificationEvent : ChannelPollBaseModel<PollBeginChoiceModel>
{
    // Empty - Nothing expect base in this event
}

/**
 * "id": "123",
 * "title": "Yeah!"
 */
public class PollBeginChoiceModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
}