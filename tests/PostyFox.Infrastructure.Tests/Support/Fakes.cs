using PostyFox.Application.Abstractions;
using PostyFox.Application.Messaging;

namespace PostyFox.Infrastructure.Tests.Support;

public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

public sealed record Published(object Message, TimeSpan? Delay);

public sealed class FakeBus : IMessageBus
{
    public List<Published> Messages { get; } = new();
    public IEnumerable<T> Of<T>() => Messages.Select(m => m.Message).OfType<T>();

    public Task PublishAsync<T>(T message, TimeSpan? delay = null, CancellationToken ct = default) where T : class
    {
        Messages.Add(new Published(message, delay));
        return Task.CompletedTask;
    }
}
