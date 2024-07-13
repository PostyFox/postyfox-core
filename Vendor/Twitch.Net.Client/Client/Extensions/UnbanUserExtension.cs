using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class UnbanUserExtension
{
    public static void UnbanUser(
        this IIrcClient client,
        ChatChannel channel,
        string user
        )
        => client.SendMessage(channel, UnbanMessage(user));

    public static void UnbanUser(
        this IIrcClient client,
        string channel,
        string user
        )
        => client.SendMessage(channel, UnbanMessage(user));

    private static string UnbanMessage(string user)
        => $"/unban {user}";
}