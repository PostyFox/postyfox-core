namespace PostyFox.Application.Messaging;

/// <summary>Marks a message type with its logical queue/routing name.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class QueueAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>
/// A message whose processing concerns a specific post/target. The messaging layer stamps these
/// ids onto the span and into Baggage so every log emitted while handling the message is searchable
/// by PostId — a user can hand a dev the post id from the UI and the dev finds all its telemetry.
/// </summary>
public interface ITraceableMessage
{
    Guid PostId { get; }
    Guid TargetId { get; }
}

/// <summary>Stage 1: render a target's content from the post + template.</summary>
[Queue("generate")]
public sealed class GenerateTargetCommand : ITraceableMessage
{
    public Guid PostId { get; set; }
    public Guid TargetId { get; set; }
}

/// <summary>Stage 2: deliver a rendered target to its platform.</summary>
[Queue("deliver")]
public sealed class DeliverTargetCommand : ITraceableMessage
{
    public Guid PostId { get; set; }
    public Guid TargetId { get; set; }
}

public static class QueueNames
{
    public static string For<T>() =>
        (Attribute.GetCustomAttribute(typeof(T), typeof(QueueAttribute)) as QueueAttribute)?.Name
        ?? typeof(T).Name;
}
