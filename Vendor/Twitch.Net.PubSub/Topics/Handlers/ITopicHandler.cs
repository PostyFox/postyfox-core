using Twitch.Net.PubSub.Client.Handlers.Events;

namespace Twitch.Net.PubSub.Topics.Handlers;

internal interface ITopicHandler
{
    bool Handle(IPubSubClientEventInvoker eventInvoker, ParsedTopicMessage message);
}