using Optional;
using Twitch.Net.Client.Extensions;
using Twitch.Net.Client.Irc;

namespace Twitch.Net.Client.Models;

public class ChannelStates
{
    private readonly Dictionary<string, string> _tags;
    public string Channel { get; }
        
    public ChannelStates(IrcMessage ircMessage)
    {
        _tags = ircMessage.InternalTags ?? new Dictionary<string, string>();
        Channel = ircMessage.Channel;
    }

    public bool IsFollowersOnly =>
        FollowerTimeRequirement.Match(
            t => t.TotalMinutes > -1,
            () => false);

    public string Language =>
        _tags.ContainsKey(TwitchIrcMessageTags.BroadcasterLang)
            ? _tags[TwitchIrcMessageTags.BroadcasterLang]
            : "unknown";

    public string RoomId =>
        _tags.ContainsKey(TwitchIrcMessageTags.RoomId)
            ? _tags[TwitchIrcMessageTags.RoomId]
            : "unknown";

    public Option<bool> EmoteOnly =>
        _tags.ContainsKey(TwitchIrcMessageTags.EmoteOnly)
            ? _tags.TagToBoolean(TwitchIrcMessageTags.EmoteOnly).Some()
            : Option.None<bool>();

    public Option<TimeSpan> FollowerTimeRequirement =>
        _tags.ContainsKey(TwitchIrcMessageTags.FollowersOnly)
            ? TimeSpan.Parse(_tags[TwitchIrcMessageTags.FollowersOnly]).Some()
            : Option.None<TimeSpan>();
        
    public Option<bool> R9K => 
        _tags.ContainsKey(TwitchIrcMessageTags.R9K)
            ? _tags.TagToBoolean(TwitchIrcMessageTags.R9K).Some()
            : Option.None<bool>();
        
    public Option<bool> SubOnly => 
        _tags.ContainsKey(TwitchIrcMessageTags.SubsOnly)
            ? _tags.TagToBoolean(TwitchIrcMessageTags.SubsOnly).Some()
            : Option.None<bool>();
        
    public Option<bool> Rituals => 
        _tags.ContainsKey(TwitchIrcMessageTags.Rituals)
            ? _tags.TagToBoolean(TwitchIrcMessageTags.Rituals).Some()
            : Option.None<bool>();
        
    public Option<int> SlowMode =>
        _tags.ContainsKey(TwitchIrcMessageTags.Slow)
            ? int.TryParse(_tags[TwitchIrcMessageTags.Slow], out var result)
                ? result.Some()
                : Option.None<int>()
            : Option.None<int>();

    internal void UpdateTags(IReadOnlyDictionary<string, string> tags)
    {
        foreach (var (key, value) in tags)
        {
            if (_tags.ContainsKey(key))
                _tags[key] = value;
            else
                _tags.Add(key, value);
        }
    }
}