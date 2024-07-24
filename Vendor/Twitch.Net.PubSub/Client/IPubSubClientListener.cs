namespace Twitch.Net.PubSub.Client;

public interface IPubSubClientListener
{
    void OnReconnected();
    void OnConnected();
    void OnDisconnected();
}