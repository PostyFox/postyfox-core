using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class SubscriberModeExtension
{
    public static void SubscribersOnlyOn(
        this IIrcClient client,
        ChatChannel channel
        )
        => client.SendMessage(channel, "/subscribers");

    public static void SubscribersOnlyOn(
        this IIrcClient client,
        string channel
        )
        => client.SendMessage(channel, "/subscribers");
        
    public static void SubscribersOnlyOff(
        this IIrcClient client,
        ChatChannel channel
        )
        => client.SendMessage(channel, "/subscribersoff");

    public static void SubscribersOnlyOff(
        this IIrcClient client,
        string channel
        )
        => client.SendMessage(channel, "/subscribersoff");
}