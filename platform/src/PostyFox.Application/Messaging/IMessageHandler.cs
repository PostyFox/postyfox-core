namespace PostyFox.Application.Messaging;

/// <summary>Handles a single message of type <typeparamref name="T"/>.</summary>
public interface IMessageHandler<in T> where T : class
{
    Task HandleAsync(T message, CancellationToken ct);
}
