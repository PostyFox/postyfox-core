using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>Derives the aggregate root status from the states of its targets.</summary>
public static class RootStatusCalculator
{
    public static PostRootStatus Compute(IReadOnlyCollection<PostTarget> targets)
    {
        if (targets.Count == 0) return PostRootStatus.Failed;

        var anyDelivered = targets.Any(t => t.Status == TargetStatus.Delivered);
        var anyFailed = targets.Any(t => t.Status == TargetStatus.Failed);
        var anyCancelled = targets.Any(t => t.Status == TargetStatus.Cancelled);
        var allTerminal = targets.All(t => t.Status is TargetStatus.Delivered or TargetStatus.Failed or TargetStatus.Cancelled);

        if (allTerminal)
        {
            // A user-cancelled target counts against a clean "Delivered" just like a failure does,
            // so a mix of delivered + cancelled surfaces as PartiallyFailed (something didn't go out).
            if (anyDelivered && (anyFailed || anyCancelled)) return PostRootStatus.PartiallyFailed;
            if (anyDelivered) return PostRootStatus.Delivered;
            if (anyFailed) return PostRootStatus.Failed;
            return PostRootStatus.Cancelled; // every target was cancelled
        }

        // Still in progress: once any target has entered delivery (or reached a terminal
        // state) the post as a whole is "delivering"; otherwise it is generating or queued.
        if (targets.Any(t => t.Status is TargetStatus.Delivering or TargetStatus.Delivered or TargetStatus.Failed))
            return PostRootStatus.Delivering;
        if (targets.Any(t => t.Status is TargetStatus.Generating or TargetStatus.Ready))
            return PostRootStatus.Generating;
        return PostRootStatus.Queued;
    }
}
