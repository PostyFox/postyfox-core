using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Optional;
using Twitch.Net.Communication.Events;
using Websocket.Client;
using Websocket.Client.Models;

namespace Twitch.Net.Communication.Clients;

internal class WebSocketClient<T> : IClient
{
    private readonly string _clientAddress;
    private readonly ILogger<WebSocketClient<T>> _logger;

    private Option<IClientListener> _clientListener = Option.None<IClientListener>();
    private IWebsocketClient _client;
    private bool _reconnecting; // to prevent "faulty" error message
    public bool IsConnected => _client?.IsRunning ?? false;

    public WebSocketClient(
        ILogger<WebSocketClient<T>> logger,
        string address
        )
    {
        _logger = logger;
        _clientAddress = address; // the server address the connection will be connecting to.
    }

    public async Task<bool> ConnectAsync()
    {
        if (_client == null)
        {
            _client = new WebsocketClient(new Uri(_clientAddress))
            {
                ReconnectTimeout = null // this is to prevent the client from auto disconnecting if we are not getting a response within X time
            };
            _client.ReconnectionHappened.Subscribe(OnReconnect);
            _client.MessageReceived.Subscribe(OnMessage);
            _client.DisconnectionHappened.Subscribe(Disconnected);
        }

        if (IsConnected)
            return true; // we are already connected, no need to connect again
            
        try
        {
            await _client.StartOrFail();
            Connected(); // trigger event and logger
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task DisconnectAsync(string msg = null)
    {
        try
        {
            // as everything else is done through the async way, we will disconnect async too.
            await _client.StopOrFail(WebSocketCloseStatus.NormalClosure, msg ?? "Closed on demand");
        }
        catch { /* just fail safe to not throw exceptions to main thread */ }
    }

    public async Task<bool> ReconnectAsync()
    {
        if (_client == null) // we cannot reconnect before we have done a first connection!
            return false;

        try
        {
            _reconnecting = true;
                
            if (IsConnected)
                await _client.ReconnectOrFail();
            else
            {
                await _client.StartOrFail();
                OnReconnect(new ReconnectionInfo(ReconnectionType.ByServer));
            }

            _reconnecting = false;
                
            return true;
        }
        catch
        {
            return await ReconnectAsync(); // if reconnect or start fails we just wanna re-try
        }
    }

    private void Connected()
    {
        _logger?.LogTrace("Connection happened");
        _clientListener.MatchSome(listener => listener.OnConnected());
    }

    private void OnReconnect(ReconnectionInfo reconnectionInfo)
    {
        // we do not want to pass "initial"
        if (reconnectionInfo.Type == ReconnectionType.Initial)
            return;
            
        _logger?.LogTrace("Reconnection happened, type: {Type}", reconnectionInfo.Type);
        _clientListener.MatchSome(listener => listener.OnReconnected());
    }

    private void OnMessage(ResponseMessage responseMessage)
    {
        _logger?.LogInformation("[INCOMING] [Type: {Type}] - {Text}", responseMessage.MessageType, responseMessage.Text.TrimEnd());
        _clientListener.MatchSome(listener => 
            listener.OnMessage(responseMessage.MessageType, responseMessage.Text));
    }

    private void Disconnected(DisconnectionInfo disconnectionInfo)
    {
        if (_reconnecting) return; // disconnects are not valid during a reconnection is going on
            
        _logger?.LogInformation("Disconnect happened - Reason: {Type}", disconnectionInfo.Type);
        _clientListener.MatchSome(listener => listener.OnDisconnected(
            new ClientDisconnected(disconnectionInfo?.CloseStatusDescription ?? string.Empty))
        );
    }

    public bool Send(string data)
    {
        if (!IsConnected)
        {
            _logger?.LogWarning("[OUTGOING] Message was not sent, client is not connected");
            return false; // if we are not connected, we will just return false
        }
            
        _client.Send(data);
        return true;
    }

    public void SetListener(IClientListener listener) =>
        _clientListener = listener.Some();
}