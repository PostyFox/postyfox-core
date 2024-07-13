using Twitch.Net.PubSub.Client.Handlers.Events;
using Twitch.Net.PubSub.Topics;

namespace Twitch.Net.PubSub.Client;

public interface IPubSubClient
{
    bool IsConnected { get; }
        
    Task<bool> ConnectAsync();
    TopicBuilder CreateBuilder();
    IPubSubClientEventHandler Events { get; }
    void Send(string data);
}