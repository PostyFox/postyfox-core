using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class SlowModeExtension
{
    public static void SlowModeOn(
        this IIrcClient client, 
        ChatChannel channel,
        TimeSpan cooldown)
        => client.SendMessage(channel, SlowModeCommand(cooldown));

    public static void SlowModeOn(
        this IIrcClient client, 
        string channel, 
        TimeSpan cooldown)
        => client.SendMessage(channel, SlowModeCommand(cooldown));

    private static string SlowModeCommand(TimeSpan cooldown)
    {
        if (cooldown.TotalDays >= 1)
            cooldown = new TimeSpan(1,0,0,0);
        else if (cooldown.TotalSeconds < 0)
            return "/slowoff";
        return $"/slow {cooldown.TotalSeconds}";
    }

    public static void SlowModeOff(
        this IIrcClient client, 
        ChatChannel channel)
        => client.SendMessage(channel, "/slowoff");

    public static void SlowModeOff(
        this IIrcClient client, 
        string channel)
        => client.SendMessage(channel, "/slowoff");
}