using OpenTelemetry;

namespace PostyFox.Application.Telemetry;

/// <summary>
/// Business-key telemetry: puts the current PostId (and optionally TargetId) into ambient Baggage so
/// downstream logs are searchable by post. Lives in Application (the layer that owns these ids) so
/// both the API intake and the worker's message consumer can set them; the log enricher in the
/// Infrastructure layer reads the same keys. Baggage flows across async, child spans, and the
/// RabbitMQ hop, and — unlike an ILogger scope — doesn't trip the disabled-scopes/duplicate-key issue.
/// </summary>
public static class PostTelemetry
{
    public const string BaggagePostId = "post.id";
    public const string BaggageTargetId = "target.id";

    /// <summary>Set the business keys on Baggage for the current async flow. Call once you own the
    /// scope (an API request, or a per-message consumer callback) to avoid leaking into other work.</summary>
    public static void SetBusinessBaggage(Guid postId, Guid? targetId = null)
    {
        var baggage = Baggage.Current.SetBaggage(BaggagePostId, postId.ToString());
        if (targetId is { } t)
            baggage = baggage.SetBaggage(BaggageTargetId, t.ToString());
        Baggage.Current = baggage;
    }
}
