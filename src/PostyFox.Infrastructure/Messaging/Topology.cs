using RabbitMQ.Client;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Declares the exchange/queue topology. The main exchange is a plain "direct" exchange (no
/// delayed-message plugin — see <see cref="RabbitMqMessageBus"/> for how retry backoff delay is
/// achieved instead). Each queue dead-letters to a per-queue DLQ on handler failure, and has a
/// companion "retry" holding queue used for delayed re-publish (see
/// <see cref="RabbitMqMessageBus.PublishAsync{T}"/>).
/// </summary>
internal static class Topology
{
    public static string Dlx(string exchange) => $"{exchange}.dlx";
    public static string Dlq(string queue) => $"{queue}.dlq";
    public static string RetryQueue(string queue) => $"{queue}.retry";

    public static async Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(exchange, "direct", durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(Dlx(exchange), "direct", durable: true, autoDelete: false, cancellationToken: ct);
    }

    public static async Task DeclareQueueAsync(IChannel channel, string exchange, string queue, CancellationToken ct)
    {
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = Dlx(exchange) }, cancellationToken: ct);
        await channel.QueueBindAsync(queue, exchange, routingKey: queue, cancellationToken: ct);

        var dlq = Dlq(queue);
        await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(dlq, Dlx(exchange), routingKey: queue, cancellationToken: ct);

        // Holding queue for delayed re-publish (retry backoff): never consumed directly. A message
        // sits here until its per-message TTL (set as the AMQP `expiration` property at publish time)
        // elapses, at which point RabbitMQ dead-letters it back into the main exchange/queue — no
        // plugin required. Safe from head-of-line blocking here because callers only ever use this
        // for DeliverTargetHandler's narrow, bounded retry backoff (10-20s) — see
        // RabbitMqMessageBus.PublishAsync and PostSchedulerService for the wider-range scheduling
        // case, which never touches RabbitMQ delay at all.
        await channel.QueueDeclareAsync(RetryQueue(queue), durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = exchange,
                ["x-dead-letter-routing-key"] = queue
            }, cancellationToken: ct);
    }
}

