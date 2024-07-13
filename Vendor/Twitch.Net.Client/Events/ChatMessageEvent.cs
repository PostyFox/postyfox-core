using Twitch.Net.Client.Extensions;
using Twitch.Net.Client.Irc;
using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Events;

public class ChatMessageEvent
{
    private readonly string _username;
    private readonly IReadOnlyDictionary<string, string> _badges;
    public IReadOnlyDictionary<string, string> Tags { get; }
    public string Message { get; }
    public bool IsMeAction { get; }
    public string Channel { get; }
        
    internal ChatMessageEvent(IrcMessage ircMessage)
    {
        Tags = ircMessage.Tags;
        _username = ircMessage.User;
        _badges = Tags.ParseBadges();
            
        Message = IsIncomingMeAction(ircMessage.Message)
            ? ircMessage.Message.Trim('\u0001').Substring(7)
            : ircMessage.Message;
        IsMeAction = IsIncomingMeAction(ircMessage.Message);
        Channel = ircMessage.Channel;
    }

    public string DisplayName => 
        Tags.ContainsKey(TwitchIrcMessageTags.DisplayName) && 
        !string.IsNullOrEmpty(Tags[TwitchIrcMessageTags.DisplayName])
            ? Tags[TwitchIrcMessageTags.DisplayName]
            : _username;

    public string Username => _username;

    public string UserId => Tags["user-id"];

    public string MessageId => Tags["id"];

    public string ChannelId => Tags["room-id"];

    public bool IsStreamer =>
        _badges.ContainsKey("broadcaster") ||
        Tags.TagToBoolean(TwitchIrcMessageTags.Broadcaster);
        
    public bool IsModerator =>
        _badges.ContainsKey("broadcaster") ||
        _badges.ContainsKey("moderator") ||
        Tags.TagToBoolean(TwitchIrcMessageTags.Mod);

    public bool IsSubscriber =>
        _badges.ContainsKey("founder") ||
        _badges.ContainsKey("subscriber") ||
        Tags.TagToBoolean(TwitchIrcMessageTags.Subscriber);

    public bool IsTurbo =>
        Tags.ParseBadges().ContainsKey("turbo") ||
        Tags.TagToBoolean(TwitchIrcMessageTags.Turbo);

    public bool IsVip =>
        _badges.ContainsKey("vip") ||
        Tags.TagToBoolean(TwitchIrcMessageTags.Vip);

    public UserType Type =>
        Tags.ContainsKey(TwitchIrcMessageTags.UserType)
            ? Tags[TwitchIrcMessageTags.UserType] switch
            {
                "mod" => UserType.Moderator,
                "global_mod" => UserType.GlobalModerator,
                "admin" => UserType.Admin,
                "staff" => UserType.Staff,
                _ => UserType.Viewer
            }
            : UserType.Viewer;

    private bool IsIncomingMeAction(string msg)
        => msg.Length > 0 && (byte) msg[0] == 1 && (byte) msg[^1] == 1 &&
           msg.StartsWith("\u0001ACTION ") && msg.EndsWith("\u0001");
}