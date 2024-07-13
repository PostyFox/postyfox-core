using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Client.Extensions;

public static class RemoveMessageExtension
{
    public static void RemoveMessage(
        this IIrcClient client,
        ChatChannel channel,
        string messageId
        )
        => client.SendMessage(channel, RemoveMessageCommand(messageId));

    public static void RemoveMessage(
        this IIrcClient client,
        string channel,
        string messageId
        )
        => client.SendMessage(channel, RemoveMessageCommand(messageId));
		
    private static string RemoveMessageCommand(string messageId) =>
        $"/delete {messageId}";
}