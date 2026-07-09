namespace PostyFox.Application.Connectors;

public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<string, IConnector> _byPlatform;

    public ConnectorRegistry(IEnumerable<IConnector> connectors)
    {
        _byPlatform = new(StringComparer.OrdinalIgnoreCase);
        foreach (var c in connectors)
            _byPlatform[c.Describe().Platform] = c;
    }

    public bool TryGet(string platform, out IConnector connector)
    {
        if (platform is not null && _byPlatform.TryGetValue(platform, out var c))
        {
            connector = c;
            return true;
        }
        connector = null!;
        return false;
    }

    public IReadOnlyCollection<IConnector> All => _byPlatform.Values;
}
