using System.Text.Json;
using Twitch.Net.PubSub.Client.Handlers.Events;
using Twitch.Net.PubSub.Events;

namespace Twitch.Net.PubSub.Topics.Handlers;

internal class RedeemTopicHandler : ITopicHandler
{
    public bool Handle(IPubSubClientEventInvoker eventInvoker, ParsedTopicMessage message)
    {
        var data = JsonSerializer.Deserialize<CommunityPointsEvent>(message.JsonData);

        if (data?.EventType == CommunityPointsEventType.Redeem)
            eventInvoker.InvokeRedeemTopic(data);
        else if (data?.EventType == CommunityPointsEventType.CustomCreated)
            eventInvoker.InvokeCustomRedeemCreatedTopic(data);
        else if (data?.EventType == CommunityPointsEventType.CustomDeleted)
            eventInvoker.InvokeCustomRedeemDeletedTopic(data);
        else if (data?.EventType == CommunityPointsEventType.CustomUpdated)
            eventInvoker.InvokeCustomRedeemUpdatedTopic(data);
        else if (data?.EventType == CommunityPointsEventType.AutomaticUpdated)
            eventInvoker.InvokeAutomaticRedeemUpdatedTopic(data);
        else if (data?.EventType == CommunityPointsEventType.InProgress)
            eventInvoker.InvokeCustomRedeemInProgressTopic(data);
        else if (data?.EventType == CommunityPointsEventType.ProgressFinished)
            eventInvoker.InvokeCustomRedeemFinishedProgressTopic(data);
        else if (data?.EventType == CommunityPointsEventType.RedemptionUpdate)
            eventInvoker.InvokeCustomRedeemStatusUpdateTopic(data);
        else
            return false; // if it is an unknown event type we did not "handle"
            
        return true;
    }
}