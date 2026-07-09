namespace PostyFox.Application.Triggers;

/// <summary>Outcome of parsing an inbound webhook body for a trigger source.</summary>
public sealed record TriggerParseResult(
    bool IsChallenge,
    string? Challenge,
    string? MessageId,
    string? ExternalAccount,
    IReadOnlyDictionary<string, string> Variables);

/// <summary>
/// A pluggable external event source (e.g. a generic signed webhook, or a future platform).
/// Encapsulates the source-specific signature scheme and payload shape so the trigger engine
/// stays source-agnostic.
/// </summary>
public interface ITriggerSource
{
    string SourceType { get; }
    bool VerifySignature(IReadOnlyDictionary<string, string> headers, string rawBody, string? signingSecret);
    TriggerParseResult Parse(IReadOnlyDictionary<string, string> headers, string rawBody);
}

public interface ITriggerSourceRegistry
{
    bool TryGet(string sourceType, out ITriggerSource source);
}

public sealed class TriggerSourceRegistry : ITriggerSourceRegistry
{
    private readonly Dictionary<string, ITriggerSource> _sources;

    public TriggerSourceRegistry(IEnumerable<ITriggerSource> sources)
    {
        _sources = new(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sources) _sources[s.SourceType] = s;
    }

    public bool TryGet(string sourceType, out ITriggerSource source)
    {
        if (sourceType is not null && _sources.TryGetValue(sourceType, out var s)) { source = s; return true; }
        source = null!;
        return false;
    }
}
