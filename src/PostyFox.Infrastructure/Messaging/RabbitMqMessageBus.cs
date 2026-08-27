using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using PostyFox.Application;
using PostyFox.Application.Messaging;
using RabbitMQ.Client;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Publishes messages to the direct exchange, ensuring the target queue exists. A non-null
/// <paramref name="delay"/> is used only for short retry backoff (see
/// <see cref="PostyFox.Application.Posting.DeliverTargetHandler"/>) — it routes the message to the
/// queue's "retry" holding queue with a per-message TTL instead of the main queue, so no delayed-
/// message-exchange plugin is required. Wider-range scheduling delay (user-scheduled posts) never
/// goes through this bus at all; it's driven by <see cref="PostyFox.Application.Posting.PostSchedulerService"/>
/// polling the database instead, to avoid head-of-line blocking a single delay queue would suffer.
/// </summary>
public sealed class RabbitMqMessageBus(RabbitMqConnection connection) : IMessageBus, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, bool> _declaredQueues = new();
    private IChannel? _channel;
    private bool _exchangeDeclared;

    public async Task PublishAsync<T>(T message, TimeSpan? delay = null, CancellationToken ct = default) where T : class
    {
        var queue = QueueNames.For<T>();
        var exchange = connection.Options.Exchange;
        var body = Encoding.UTF8.GetBytes(Json.Serialize(message));

        // Producer span: parents the consumer span on the other side of the queue, so the whole
        // publish→consume→handle chain is one trace. Injected into the message headers below.
        using var activity = MessagingTelemetry.Source.StartActivity($"{queue} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.operation", "publish");
        if (message is ITraceableMessage tm)
            MessagingTelemetry.TagSpan(activity, tm.PostId, tm.TargetId);

        await _gate.WaitAsync(ct);
        try
        {
            var channel = await EnsureChannelAsync(ct);
            if (!_exchangeDeclared)
            {
                await Topology.DeclareExchangeAsync(channel, exchange, ct);
                _exchangeDeclared = true;
            }
            if (_declaredQueues.TryAdd(queue, true))
                await Topology.DeclareQueueAsync(channel, exchange, queue, ct);

            var props = new BasicProperties { Persistent = true, Headers = new Dictionary<string, object?>() };
            // Carry the current trace context (traceparent/tracestate/baggage) with the message.
            MessagingTelemetry.Inject(activity, props.Headers);

            var delayMs = delay is { TotalMilliseconds: > 0 } d ? (int)d.TotalMilliseconds : 0;
            if (delayMs > 0)
            {
                // Publish directly to the retry holding queue (default exchange, routing key = queue
                // name) with a per-message TTL; it dead-letters back to the main exchange/queue once
                // that elapses. Bypasses the main exchange entirely for this hop.
                props.Expiration = delayMs.ToString();
                await channel.BasicPublishAsync(exchange: "", routingKey: Topology.RetryQueue(queue),
                    mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
            }
            else
            {
                await channel.BasicPublishAsync(exchange, routingKey: queue, mandatory: false,
                    basicProperties: props, body: body, cancellationToken: ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        var conn = await connection.GetAsync(ct);
        _channel = await conn.CreateChannelAsync(cancellationToken: ct);
        _exchangeDeclared = false;
        _declaredQueues.Clear();
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _gate.Dispose();
    }
}
