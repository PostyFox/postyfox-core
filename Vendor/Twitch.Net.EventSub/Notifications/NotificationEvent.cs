using System.Text.Json.Serialization;
using Twitch.Net.EventSub.Models;

namespace Twitch.Net.EventSub.Notifications;

public class NotificationEvent<T> : INotificationEvent
{
    [JsonPropertyName("subscription")]
    public SubscribeCallbackSubscriptionModel Subscription { get; init; } = null!;

    [JsonPropertyName("event")]
    public T Event { get; init; } = default!;
}