using RabbitMQ.Client;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Declares the delayed-exchange topology. The main exchange is an x-delayed-message
/// exchange (RabbitMQ delayed-message-exchange plugin) enabling scheduled + backoff delivery.
/// Each queue dead-letters to a per-queue DLQ.
/// </summary>
internal static class Topology
{
    public static string Dlx(string exchange) => $"{exchange}.dlx";
    public static string Dlq(string queue) => $"{queue}.dlq";

    public static async Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(
            exchange, "x-delayed-message", durable: true, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-delayed-type"] = "direct" }, cancellationToken: ct);
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
    }
}
