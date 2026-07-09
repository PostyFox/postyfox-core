using System.Collections.Concurrent;
using System.Text;
using PostyFox.Application;
using PostyFox.Application.Messaging;
using RabbitMQ.Client;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>Publishes messages to the delayed exchange, ensuring the target queue exists.</summary>
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

            var props = new BasicProperties { Persistent = true };
            var delayMs = delay is { TotalMilliseconds: > 0 } d ? (int)d.TotalMilliseconds : 0;
            if (delayMs > 0)
                props.Headers = new Dictionary<string, object?> { ["x-delay"] = delayMs };

            await channel.BasicPublishAsync(exchange, routingKey: queue, mandatory: false,
                basicProperties: props, body: body, cancellationToken: ct);
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
