namespace PostyFox.Application.Connectors;

/// <summary>
/// Uniform contract every platform integration implements (in C# in-process, or in Node
/// behind an HTTP adapter). Adding a platform = implement this + register a service definition.
/// </summary>
public interface IConnector
{
    ConnectorDescriptor Describe();
    Task<AuthState> IsAuthenticatedAsync(ConnectorContext context, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext context, CancellationToken ct = default);
    Task<DeliveryResult> DeliverAsync(ConnectorContext context, RenderedPost post, CancellationToken ct = default);
}

/// <summary>Resolves connectors by platform key.</summary>
public interface IConnectorRegistry
{
    bool TryGet(string platform, out IConnector connector);
    IReadOnlyCollection<IConnector> All { get; }
}
