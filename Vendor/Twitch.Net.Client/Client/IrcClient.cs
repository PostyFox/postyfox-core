using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twitch.Net.Client.Client.Handlers.Events;
using Twitch.Net.Client.Client.Handlers.Join;
using Twitch.Net.Client.Configurations;
using Twitch.Net.Client.Events;
using Twitch.Net.Client.Events.Handlers;
using Twitch.Net.Client.Irc;
using Twitch.Net.Client.Models;
using Twitch.Net.Communication;
using Twitch.Net.Communication.Clients;
using Twitch.Net.Communication.Events;
using Twitch.Net.Shared.RateLimits;

namespace Twitch.Net.Client.Client;

internal class IrcClient : IIrcClient, IClientListener
{
    // inner state to make sure an event is not fired more than once
    private bool _authenticated;
        
    private readonly ILogger<IIrcClient> _logger;
    private readonly IClient _connectionClient;
    private readonly IrcCredentialConfig _config;
    private readonly IrcClientEventHandler _eventHandler;
    private readonly JoinQueueHandler _joinChannelHandler;
    private readonly List<ChatChannel> _channels = new();
        
    // Event handlers
    private readonly UserJoinedEventHandler _userJoinedEventHandler;
    private readonly UserLeftEventHandler _userLeftEventHandler;
    private readonly MessageEventHandler _messageEventHandler;
    private readonly ChatChannelStateEventHandler _chatChannelStateEventHandler;

    public IrcClient(
        IOptions<IrcCredentialConfig> config,
        ILoggerFactory loggerFactory,
        ILogger<IrcClient> logger,
        IClientFactory factory,
        IUserAccountStatusResolver accountStatusResolver
        )
    {
        _logger = logger;
        _config = config.Value;

        // Setup client handlers
        _eventHandler = new IrcClientEventHandler();
        _joinChannelHandler = new JoinQueueHandler(this, accountStatusResolver);
            
        // Setup event handlers
        _userJoinedEventHandler = new UserJoinedEventHandler(this);
        _userLeftEventHandler = new UserLeftEventHandler(this, channel => _channels.Remove(channel));
        _messageEventHandler = new MessageEventHandler();
        _chatChannelStateEventHandler = new ChatChannelStateEventHandler(this);
            
        // Create connection
        _connectionClient = factory.CreateClient<IrcClient>(loggerFactory, IrcClientAddressBuilder.CreateAddress());
        _connectionClient.SetListener(this);
    }

    public IReadOnlyList<ChatChannel> Channels
        => _channels;

    public IIrcClientEventHandler Events
        => _eventHandler;

    public bool IsConnected
        => _connectionClient.IsConnected;

    public string BotUsername
        => _config.Username;

    public async Task<bool> ConnectAsync()
    {
        if (string.IsNullOrEmpty(_config.OAuth) || string.IsNullOrEmpty(_config.Username))
            throw new ArgumentException("OAuth or Username configuration is null or empty.");
            
        return await _connectionClient.ConnectAsync();
    }

    public async Task DisconnectAsync(string custom = null) =>
        await _connectionClient.DisconnectAsync(custom);

    /**
         * Will only perform a reconnect if the client does not have a connection.
         */
    public async Task<bool> ReconnectAsync() =>
        await _connectionClient.ReconnectAsync();

    public bool SendMessage(string channel, string message) => SendMessage(
        _channels.FirstOrDefault(x => x.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase)), 
        message);

    public bool SendMessage(ChatChannel chatChannel, string message)
    {
        if (string.IsNullOrEmpty(message) || 
            chatChannel == null || 
            string.IsNullOrEmpty(_config?.Username))
            return false;
            
        if (message.Length > 500)
        {
            _logger.LogError("SendMessage cannot take a message longer than 500 characters long");
            return false;
        }

        var outbound = new OutboundChatMessage
        {
            Channel = chatChannel.ChannelName,
            Message = message,
            Username = _config.Username
        };

        return _connectionClient.Send(outbound.ToString()); // this can return false if connection is closed.
    }

    public bool SendRaw(string message)
        => _connectionClient.Send(message);

    public ChatChannel JoinChannel(string channel)
    {
        if (string.IsNullOrEmpty(channel)) // just to prevent someone from even try this.
            return new ChatChannel {ChannelName = channel}.SetConnectionState(ChatChannelConnectionState.Failure);

        if (_channels.Any(x => x.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase)))
        {
            var ch = _channels.First(x => x.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));
            if (ch.ConnectionState is ChatChannelConnectionState.Failure 
                or ChatChannelConnectionState.Left 
                or ChatChannelConnectionState.NotDefined)
                _channels.Remove(ch);
            else
                return _channels.First(x => x.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));
        }
            
        var chat = new ChatChannel
        {
            ChannelName = channel
        };

        if (!_connectionClient.IsConnected)
            return chat.SetConnectionState(ChatChannelConnectionState.Failure);

        _channels.Add(chat);
        _joinChannelHandler.Enqueue(chat, () => // on failure we wanna set the status and remove it from the list
        {
            chat.SetConnectionState(ChatChannelConnectionState.Failure);
            _channels.Remove(chat);
            _eventHandler.InvokeOnFailedChannelJoined(new FailedJoinedChannelEvent
            {
                Username = chat.ChannelName,
                Channel = chat
            });
        });
            
        return chat;
    }

    public ChatChannel LeaveChannel(string channel)
    {
        if (string.IsNullOrEmpty(channel)) // just to prevent someone from even try this.
            return new ChatChannel {ChannelName = channel}.SetConnectionState(ChatChannelConnectionState.Failure);
            
        if (!_channels.Any(x => x.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase))) 
            return new ChatChannel {ChannelName = channel}.SetConnectionState(ChatChannelConnectionState.Failure);
            
        var chatChannel = _channels.FirstOrDefault(x => x.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));
        if (chatChannel == null)
            return new ChatChannel {ChannelName = channel}.SetConnectionState(ChatChannelConnectionState.Failure);
            
        if (!_connectionClient.IsConnected)
            return chatChannel.SetConnectionState(ChatChannelConnectionState.Left);

        if (SendRaw(Rfc2812Parser.Part($"#{chatChannel.ChannelName.ToLower()}")))
            return chatChannel.SetConnectionState(ChatChannelConnectionState.Left);
            
        return chatChannel.SetConnectionState(ChatChannelConnectionState.Failure);
    }

    /**
         * Authenticate with twitch IRC server as specified : https://dev.twitch.tv/docs/irc/guide
         */
    private void AuthenticateConnection()
    {
        _authenticated = false; // before we send auths, we reset it to false so the event will re-fire
        _connectionClient.Send(Rfc2812Parser.Pass(_config.OAuth));
        _connectionClient.Send(Rfc2812Parser.Nick(_config.Username));

        _connectionClient.Send("CAP REQ twitch.tv/membership");
        _connectionClient.Send("CAP REQ twitch.tv/commands");
        _connectionClient.Send("CAP REQ twitch.tv/tags");
    }
        
    public void OnConnected() 
    {
        AuthenticateConnection();
        _eventHandler.InvokeOnIrcConnected();
    }

    public void OnReconnected()
    {
        AuthenticateConnection();
        _eventHandler.InvokeOnIrcReconnect();
        _eventHandler.InvokeOnIrcConnected();
    }

    public void OnDisconnected(ClientDisconnected clientDisconnected)
    {
        // Channel joining / joined needs to be reset since we lost connection
        _joinChannelHandler.Reset();
        _channels.Clear();
            
        _eventHandler.InvokeOnIrcDisconnect(clientDisconnected);
    }
        
    private const string MessageStringSeparator = "\r\n";
    public async Task OnMessage(WebSocketMessageType messageType, string messages)
    {
        try
        {
            if (messageType == WebSocketMessageType.Close) // if server sends "close" message we reconnect
                await _connectionClient.ReconnectAsync();
            else if (messageType == WebSocketMessageType.Text) // if it is text, then we parse & handle it
            {
                var lines = messages.Split(MessageStringSeparator);
                foreach (var line in lines.Where(l => l.Length > 1)) // one line equals one message
                    await OnHandleMessage(line);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnMessage failure");
        }
    }

    private async Task OnHandleMessage(string message)
    {
        try
        {
            var parsed = RawIrcMessageParser.ParseRawIrcMessage(message);

            // if we do not define what command type, we can disconnect a random bot by typing it in the chat
            if (parsed.Message.Contains("Login authentication failed", StringComparison.OrdinalIgnoreCase) &&
                parsed.Command == IrcCommand.Notice)
            {
                await _connectionClient.DisconnectAsync("Login authentication failed");
                return;
            }

            // the twitch server responds with "001, 002, 003, 004, 375, 372, 376" after a valid authentication
            // so we just take one of those to push an event for a developer to handle :) 
            if (parsed.Command == IrcCommand.Rpl001 && !_authenticated)
            {
                _authenticated = true;
                _eventHandler.InvokeOnAuthenticated(new TwitchAuthenticatedEvent
                {
                    Username = parsed.User
                });
                return;
            }

            var handled = parsed.Command switch
            {
                IrcCommand.Join => _userJoinedEventHandler.Handle(_eventHandler, parsed),
                IrcCommand.Part => _userLeftEventHandler.Handle(_eventHandler, parsed),
                IrcCommand.PrivMsg => _messageEventHandler.Handle(_eventHandler, parsed),
                IrcCommand.RoomState => _chatChannelStateEventHandler.Handle(_eventHandler, parsed),
                IrcCommand.Ping => SendRaw("PONG"),
                _ => false
            };

            if (!handled)
            {
                // if anyone wants to know what the output data was
                // and easier if you wanna implement a missing feature too
                _eventHandler.InvokeOnUnknownMessage(new UnknownMessageEvent
                {
                    Parsed = parsed,
                    Raw = message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handling message failed"); // we wanna have a catcher PER message that got parsed too.
        }
    }
}