using System.Text.Json;
using Twitch.Net.PubSub.Client.Handlers.Events;
using Twitch.Net.PubSub.Events;

namespace Twitch.Net.PubSub.Topics.Handlers;

internal class SubscribeTopicHandler : ITopicHandler
{
    public bool Handle(IPubSubClientEventInvoker eventInvoker, ParsedTopicMessage message)
    {
        var data = JsonSerializer.Deserialize<SubscribeEvent>(message.JsonData);
            
        if (data?.Gifted ?? false)
            eventInvoker.InvokeGiftedSubscriptionEventTopic(data);
        else if (data != null)
            eventInvoker.InvokeSubscriptionEventTopic(data);
        else
            return false;
            
        return true;
    }
}