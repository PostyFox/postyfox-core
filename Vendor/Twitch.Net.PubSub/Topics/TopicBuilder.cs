using Microsoft.Extensions.Options;
using Twitch.Net.PubSub.Client;
using Twitch.Net.PubSub.Configurations;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.PubSub.Topics;

public class TopicBuilder
{
    private const string ValidRandomCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly Random Gen = new();
        
    private readonly IPubSubClient _client;
    private readonly List<string> _topics = new();
    private readonly PubSubCredentialConfig _config;

    public TopicBuilder(
        IPubSubClient client,
        IOptions<PubSubCredentialConfig> config
        )
    {
        _client = client;
        _config = config.Value;
    }
        
    private TopicBuilder AddTopic(string topic)
    {
        _topics.Add(topic);
        return this;
    }

    public TopicBuilder CreateChannelBitsCheerTopic(long userId)
        => AddTopic($"channel-bits-events-v2.{userId}");
        
    public TopicBuilder CreateChannelSubscribeEventsTopic(long userId)
        => AddTopic($"channel-subscribe-events-v1.{userId}");
        
    public TopicBuilder CreateChannelPointsRedeemTopic(long userId)
        => AddTopic($"channel-points-channel-v1.{userId}");
        
    /**
     * Overload if you wanna pass a specific token otherwise it'll take the configured one.
     */
    public void Listen(string token = null)
        => SendTopics(true, token);

    /**
     * Overload if you wanna pass a specific token otherwise it'll take the configured one.
     */
    public void Unlisten(string token = null)
        => SendTopics(true, token);

    private void SendTopics(bool listen = true, string token = null)
    {
        if (_topics.Count == 0)
            return;

        if (string.IsNullOrEmpty(token))
            token = _config.OAuth;

        var nonce = GenerateNonce();

        var data = new TopicDataModel
        {
            Topics = _topics,
            Token = token?.Replace("oauth:", "", StringComparison.OrdinalIgnoreCase)
        };

        var model = new TopicModel
        {
            Type = listen ? "LISTEN" : "UNLISTEN",
            Nonce = nonce,
            Data = data
        };

        _client.Send(JsonSerializerHelper.SerializeModel(model));
    }

    private static string GenerateNonce() =>
        new(Enumerable.Repeat(ValidRandomCharacters, 18)
            .Select(s => s[Gen.Next(s.Length)]).ToArray());
}