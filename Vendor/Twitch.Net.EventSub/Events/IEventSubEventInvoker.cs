using Twitch.Net.EventSub.Notifications;

namespace Twitch.Net.EventSub.Events;

public interface IEventSubEventInvoker
{
    void InvokeNotification(INotificationEvent @event, string type);
}