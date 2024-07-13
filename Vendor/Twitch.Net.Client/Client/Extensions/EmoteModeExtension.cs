using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class EmoteModeExtension
{
    public static void EmoteOnlyOn(
        this IIrcClient client,
        ChatChannel channel
        )
        => client.SendMessage(channel, "/emoteonly");

    public static void EmoteOnlyOn(
        this IIrcClient client,
        string channel
        )
        => client.SendMessage(channel, "/emoteonly");
        
    public static void EmoteOnlyOff(
        this IIrcClient client,
        ChatChannel channel
        )
        => client.SendMessage(channel, "/emoteonlyoff");

    public static void EmoteOnlyOff(
        this IIrcClient client,
        string channel
        )
        => client.SendMessage(channel, "/emoteonlyoff");
}