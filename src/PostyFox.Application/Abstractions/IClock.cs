namespace PostyFox.Application.Abstractions;

/// <summary>Abstraction over the system clock for deterministic testing.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
