using System.Text.Json;
using Twitch.Net.PubSub.Client.Handlers.Events;
using Twitch.Net.PubSub.Events;

namespace Twitch.Net.PubSub.Topics.Handlers;

internal class CheerTopicHandler : ITopicHandler
{
    public bool Handle(IPubSubClientEventInvoker eventInvoker, ParsedTopicMessage message)
    {
        var data = JsonSerializer.Deserialize<CheerEvent>(message.JsonData);

        // Both anonymous & none anonymous
        eventInvoker.InvokeCheerTopic(data);
            
        return true;
    }
}