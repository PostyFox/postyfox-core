namespace Twitch.Net.Client.Client;

public static class IrcClientAddressBuilder
{
    private const string IrcServerAddress = "irc-ws.chat.twitch.tv";

    public static string CreateAddress(bool ssl = true) =>
        $"{Protocol(ssl)}://{IrcServerAddress}:{Port(ssl)}";
        
    private static string Protocol(bool ssl) => ssl ? "wss" : "ws";
    private static string Port(bool ssl) => ssl ? "443" : "80";
}