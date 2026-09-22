namespace PostyFox.Domain.Enums;

/// <summary>Status of a single per-platform delivery target.</summary>
public enum TargetStatus
{
    Queued = 0,
    Generating = 1,
    Ready = 2,
    Delivering = 3,
    Delivered = 4,
    Failed = 5,
    /// <summary>Cancelled by the user before it was delivered. Terminal; handlers skip it.</summary>
    Cancelled = 6
}

/// <summary>Aggregated status of a root post across all its targets.</summary>
public enum PostRootStatus
{
    Queued = 0,
    Generating = 1,
    Delivering = 2,
    Delivered = 3,
    PartiallyFailed = 4,
    Failed = 5,
    /// <summary>Every remaining target was cancelled by the user. Terminal.</summary>
    Cancelled = 6,
    /// <summary>Saved by the user but not yet submitted; has no targets/queue activity until published.</summary>
    Draft = 7
}

/// <summary>Audience/content classification supplied by the author for platforms that require it.</summary>
public enum ContentRating
{
    General = 0,
    Mature = 1,
    Adult = 2,
    Extreme = 3
}

/// <summary>
/// A post-delivery action a <see cref="Entities.PostTargetAutomation"/> can take against an
/// already-delivered target (issue #323: "automation").
/// </summary>
public enum AutomationAction
{
    /// <summary>Repost/reblog/boost the delivered target on its own platform.</summary>
    Repost = 0,
    /// <summary>Delete the delivered target from its platform.</summary>
    Delete = 1
}

/// <summary>Lifecycle of a single <see cref="Entities.PostTargetAutomation"/> rule.</summary>
public enum AutomationStatus
{
    /// <summary>Not yet run: either still waiting for its target to deliver, or waiting for its due time.</summary>
    Pending = 0,
    /// <summary>Ran successfully.</summary>
    Done = 1,
    /// <summary>Ran and failed (see <see cref="Entities.PostTargetAutomation.Error"/>).</summary>
    Failed = 2,
    /// <summary>Cancelled by the user, or by its target being cancelled, before it ran.</summary>
    Cancelled = 3
}

/// <summary>Lifecycle of a single <see cref="Entities.AccountInvite"/> (issue #409: account delegation).</summary>
public enum InviteStatus
{
    /// <summary>Sent, not yet accepted, revoked or expired.</summary>
    Pending = 0,
    /// <summary>Accepted; an <see cref="Entities.AccountMember"/> row now exists for it.</summary>
    Accepted = 1,
    /// <summary>Revoked by the inviting owner before it was accepted.</summary>
    Revoked = 2
}
