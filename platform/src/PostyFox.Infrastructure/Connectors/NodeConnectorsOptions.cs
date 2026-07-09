namespace PostyFox.Infrastructure.Connectors;

public sealed class NodeConnectorsOptions
{
    public const string SectionName = "NodeConnectors";
    public string BaseUrl { get; set; } = "http://connectors-node:8090";
    /// <summary>Shared internal token sent as X-Internal-Token to the Node service.</summary>
    public string? InternalToken { get; set; }
}
