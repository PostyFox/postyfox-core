using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class FollowerModeExtension
{
    private const int MaximumDurationAllowedDays = 90;
        
    public static void FollowerModeOn(
        this IIrcClient client, 
        ChatChannel channel,
        TimeSpan cooldown
        )
        => client.SendMessage(channel, FollowersCommand(cooldown));
        

    public static void FollowerModeOn(
        this IIrcClient client, 
        string channel, 
        TimeSpan cooldown
        )
        => client.SendMessage(channel, FollowersCommand(cooldown));

    private static string FollowersCommand(TimeSpan cooldown)
    {
        if (cooldown.TotalDays >= MaximumDurationAllowedDays)
            cooldown = new TimeSpan(90, 0, 0, 0);
        else if (cooldown.TotalSeconds < 0)
            cooldown = new TimeSpan(0);
        return $"/followers {cooldown.TotalSeconds}";
    }

    public static void FollowerModeOff(
        this IIrcClient client, 
        ChatChannel channel
        )
        => client.SendMessage(channel, "/followersoff");

    public static void FollowerModeOff(
        this IIrcClient client, 
        string channel
        )
        => client.SendMessage(channel, "/followersoff");
}