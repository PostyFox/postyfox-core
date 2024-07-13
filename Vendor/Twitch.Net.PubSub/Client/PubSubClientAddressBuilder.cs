namespace Twitch.Net.PubSub.Client;

public static class PubSubClientAddressBuilder
{
    private const string PubSubServerAddress = "pubsub-edge.twitch.tv";
        
    public static string CreateAddress(bool ssl = true) =>
        $"{Protocol(ssl)}://{PubSubServerAddress}:{Port(ssl)}";
        
    private static string Protocol(bool ssl) => ssl ? "wss" : "ws";
    private static string Port(bool ssl) => ssl ? "443" : "80";
}