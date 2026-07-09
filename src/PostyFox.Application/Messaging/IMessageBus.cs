namespace PostyFox.Application.Messaging;

/// <summary>
/// Cloud-agnostic message bus (RabbitMQ by default). Supports optional delayed delivery
/// which the pipeline uses for scheduling and retry backoff.
/// </summary>
public interface IMessageBus
{
    Task PublishAsync<T>(T message, TimeSpan? delay = null, CancellationToken ct = default) where T : class;
}
