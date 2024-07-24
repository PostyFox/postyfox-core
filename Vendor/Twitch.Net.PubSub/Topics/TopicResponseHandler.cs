using System.Text.Json;
using Twitch.Net.PubSub.Client.Handlers.Events;
using Twitch.Net.PubSub.Events;
using Twitch.Net.PubSub.Topics.Handlers;

namespace Twitch.Net.PubSub.Topics;

public class TopicResponseHandler
{
    private readonly IPubSubClientEventInvoker _eventInvoker;
    private readonly ITopicHandler _redeemTopicHandler = new RedeemTopicHandler();
    private readonly ITopicHandler _cheerTopicHandler = new CheerTopicHandler();
    private readonly ITopicHandler _subscribeTopicHandler = new SubscribeTopicHandler();

    public TopicResponseHandler(IPubSubClientEventInvoker eventInvoker)
    {
        _eventInvoker = eventInvoker;
    }

    public bool Handle(string type, Dictionary<string, object> parsed) =>
        type switch
        {
            "response" => HandleResponseMessage(parsed),
            "message" => HandleMessageTopic(ParseMessage(parsed)),
            _ => false
        };

    private static ParsedTopicMessage ParseMessage(Dictionary<string, object> parsed)
    {
        if (parsed.ContainsKey("data") && parsed["data"] is JsonElement { ValueKind: JsonValueKind.Object } element) 
        {
            try
            {
                var topic = element.GetProperty("topic").GetString()?.Split(".")[0];
                var data = element.GetProperty("message").GetString();
                    
                if (!string.IsNullOrEmpty(topic) && !string.IsNullOrEmpty(data))
                    return new ParsedTopicMessage
                    {
                        Parsed = true,
                        Topic = topic,
                        JsonData = data
                    };
            } catch { /* Will be sent as a "UnknownMessageEvent" if it failed */ }
        }
        return new ParsedTopicMessage();
    }

    private bool HandleMessageTopic(ParsedTopicMessage message)
    {
        if (!message.Parsed) // so if the parsing went wrong, we will return false to trigger "UnknownMessageEvent"
            return false;

        try
        {
            return message.Topic switch
            {
                "channel-points-channel-v1" => _redeemTopicHandler.Handle(_eventInvoker, message),
                "channel-bits-events-v2" => _cheerTopicHandler.Handle(_eventInvoker, message),
                "channel-subscribe-events-v1" => _subscribeTopicHandler.Handle(_eventInvoker, message),
                _ => false
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Handled messaged topic failed : {ex.Message} \n [RAW] : {message.JsonData}");
            return false;
        }
    }

    private bool HandleResponseMessage(Dictionary<string, object> parsed)
    {
        if (parsed.ContainsKey("nonce") && parsed.ContainsKey("error"))
        {
            _eventInvoker.InvokeResponseMessage(new MessageResponse
            {
                Nonce = parsed["nonce"].ToString(),
                Error = parsed["error"].ToString()
            });
            return true;
        }
            
        return false;
    }
}