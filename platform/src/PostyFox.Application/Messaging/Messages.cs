namespace PostyFox.Application.Messaging;

/// <summary>Marks a message type with its logical queue/routing name.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class QueueAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>Stage 1: render a target's content from the post + template.</summary>
[Queue("generate")]
public sealed class GenerateTargetCommand
{
    public Guid PostId { get; set; }
    public Guid TargetId { get; set; }
}

/// <summary>Stage 2: deliver a rendered target to its platform.</summary>
[Queue("deliver")]
public sealed class DeliverTargetCommand
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
