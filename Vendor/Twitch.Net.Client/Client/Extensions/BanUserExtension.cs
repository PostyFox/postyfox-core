using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class BanUserExtension
{
    public static void BanUser(
        this IIrcClient client,
        ChatChannel channel,
        string user,
        string reason
        )
        => client.SendMessage(channel, BanMessage(user, reason));

    public static void BanUser(
        this IIrcClient client,
        string channel,
        string user,
        string reason
        )
        => client.SendMessage(channel, BanMessage(user, reason));

    private static string BanMessage(string user, string reason)
        => $"/ban {user} {reason}";
}