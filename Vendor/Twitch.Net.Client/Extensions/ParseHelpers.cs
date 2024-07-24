using Twitch.Net.Client.Irc;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.Client.Extensions;

public static class ParseHelpers
{
    public static bool TagToBoolean(this IReadOnlyDictionary<string, string> tags, string key)
        => tags != null && tags.ContainsKey(key) && tags[key].Equals("1") 
           || tags != null && tags.ContainsKey(key) && int.TryParse(tags[key], out var value) && value > 0;

    public static IReadOnlyDictionary<string, string> ParseBadges(this IReadOnlyDictionary<string, string> tags)
    {
        var output = new Dictionary<string, string>();

        if (tags != null && 
            tags.ContainsKey(TwitchIrcMessageTags.Badges) &&
            tags[TwitchIrcMessageTags.Badges].Contains('/'))
        {
            tags[TwitchIrcMessageTags.Badges]
                .Split(',')
                .ForEach(badge =>
                {
                    var split = badge.Split('/');
                    output.Add(split[0], split[1]);
                });
        }
            
        return output;
    }
}