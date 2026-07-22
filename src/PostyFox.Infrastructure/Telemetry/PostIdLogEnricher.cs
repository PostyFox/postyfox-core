using OpenTelemetry;
using OpenTelemetry.Logs;
using PostyFox.Application.Telemetry;

namespace PostyFox.Infrastructure.Telemetry;

/// <summary>
/// Stamps the current PostId (and TargetId) from Baggage onto every log record as attributes, so
/// logs are searchable by post in OpenSearch (<c>log.attributes.PostId</c>). A user can hand a dev
/// the post id shown in the UI and the dev finds all its telemetry.
///
/// Baggage — not an ILogger scope — because scopes are disabled globally (they caused the ASP.NET
/// duplicate-key rejection at Data Prepper), whereas Baggage flows across async, child spans, and
/// the RabbitMQ hop. No-op when no post is in context (e.g. health checks, startup logs), so it
/// never adds empty attributes.
/// </summary>
public sealed class PostIdLogEnricher : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        var postId = Baggage.Current.GetBaggage(PostTelemetry.BaggagePostId);
        if (string.IsNullOrEmpty(postId)) return;

        var attrs = data.Attributes is null
            ? new List<KeyValuePair<string, object?>>()
            : new List<KeyValuePair<string, object?>>(data.Attributes);

        // A handler may already log {PostId} structurally — don't double-stamp (avoids the
        // duplicate-key rejection at Data Prepper).
        if (attrs.Exists(a => a.Key == "PostId")) return;

        attrs.Add(new KeyValuePair<string, object?>("PostId", postId));
        var targetId = Baggage.Current.GetBaggage(PostTelemetry.BaggageTargetId);
        if (!string.IsNullOrEmpty(targetId) && !attrs.Exists(a => a.Key == "TargetId"))
            attrs.Add(new KeyValuePair<string, object?>("TargetId", targetId));

        data.Attributes = attrs;
    }
}
