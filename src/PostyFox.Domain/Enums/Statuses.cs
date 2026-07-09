namespace PostyFox.Domain.Enums;

/// <summary>Status of a single per-platform delivery target.</summary>
public enum TargetStatus
{
    Queued = 0,
    Generating = 1,
    Ready = 2,
    Delivering = 3,
    Delivered = 4,
    Failed = 5
}

/// <summary>Aggregated status of a root post across all its targets.</summary>
public enum PostRootStatus
{
    Queued = 0,
    Generating = 1,
    Delivering = 2,
    Delivered = 3,
    PartiallyFailed = 4,
    Failed = 5
}
