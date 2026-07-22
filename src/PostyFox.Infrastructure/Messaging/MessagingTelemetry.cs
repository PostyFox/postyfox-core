using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Carries W3C trace context across the RabbitMQ hop so a message's processing is stitched into the
/// trace that produced it. HTTP hops propagate automatically (ASP.NET Core + HttpClient
/// instrumentation), but a queue is opaque to that machinery: the producer must <see cref="Inject"/>
/// the current context into the message headers and the consumer must <see cref="Extract"/> it and
/// start a span parented to it. With a span active, every log emitted during handling inherits its
/// traceId/spanId — which is why worker logs were previously orphaned (empty traceId).
/// </summary>
public static class MessagingTelemetry
{
    public const string ActivitySourceName = "PostyFox.Messaging";
    public static readonly ActivitySource Source = new(ActivitySourceName);

    // W3C TraceContext + Baggage (the SDK default). Shared so producer/consumer agree on the format.
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>Tag a span with the business identifiers (searchable/filterable on the trace).
    /// The searchable-in-logs half lives in <see cref="PostyFox.Application.Telemetry.PostTelemetry"/>.</summary>
    public static void TagSpan(Activity? activity, Guid postId, Guid targetId)
    {
        activity?.SetTag("postyfox.post.id", postId);
        activity?.SetTag("postyfox.target.id", targetId);
    }

    /// <summary>Write the active trace context into a header dictionary. RabbitMQ header values are
    /// carried as UTF-8 byte arrays by convention, so encode accordingly.</summary>
    public static void Inject(Activity? activity, IDictionary<string, object?> headers)
    {
        var context = new PropagationContext(
            activity?.Context ?? Activity.Current?.Context ?? default,
            Baggage.Current);
        Propagator.Inject(context, headers,
            static (h, key, value) => h[key] = Encoding.UTF8.GetBytes(value));
    }

    /// <summary>Read the trace context a producer injected into the message headers.</summary>
    public static PropagationContext Extract(IReadOnlyBasicProperties props) =>
        Propagator.Extract(default, props, static (p, key) =>
        {
            if (p.Headers is not null && p.Headers.TryGetValue(key, out var value) && value is byte[] bytes)
                return new[] { Encoding.UTF8.GetString(bytes) };
            return Array.Empty<string>();
        });
}
