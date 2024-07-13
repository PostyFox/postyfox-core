using System.Net.WebSockets;
using Twitch.Net.Communication.Events;

namespace Twitch.Net.Communication.Clients;

public interface IClientListener
{
    void OnReconnected();
    Task OnMessage(WebSocketMessageType messageType, string message);
    void OnConnected();
    void OnDisconnected(ClientDisconnected clientDisconnected);
}