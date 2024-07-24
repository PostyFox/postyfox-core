using Optional;

namespace Twitch.Net.Client.Models;

public class ChatChannel
{
    public string ChannelName { get; init; }
    public ChatChannelConnectionState ConnectionState { get; private set; } = ChatChannelConnectionState.NotDefined;
    public Option<ChannelStates> States { get; private set; } = Option.None<ChannelStates>();

    // is update depending on the result of queue and what not
    internal ChatChannel SetConnectionState(ChatChannelConnectionState state)
    {
        ConnectionState = state;
        return this;
    }

    internal ChatChannel SetChannelStates(Option<ChannelStates> states)
    {
        States = states;
        return this;
    }

    internal ChatChannel UpdateTags(IReadOnlyDictionary<string, string> tags)
    {
        States.MatchSome(state => state.UpdateTags(tags));
        return this;
    }
}

/**
 * If a channel is set to "NotDefined" / "Left" / "Failure" the instance of the "ChatChannel" is dead
 * A new connection attempt toward the chat will create a new instance
 */
public enum ChatChannelConnectionState
{
    NotDefined,
    Queued,
    Connected,
    Left,
    Failure,
    Connecting
}