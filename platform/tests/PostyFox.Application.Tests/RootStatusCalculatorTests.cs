using PostyFox.Application.Posting;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class RootStatusCalculatorTests
{
    private static PostTarget T(TargetStatus s) => new() { Status = s };

    [Fact] public void Empty_is_failed() => Assert.Equal(PostRootStatus.Failed, RootStatusCalculator.Compute([]));

    [Fact] public void All_delivered() =>
        Assert.Equal(PostRootStatus.Delivered, RootStatusCalculator.Compute([T(TargetStatus.Delivered), T(TargetStatus.Delivered)]));

    [Fact] public void All_failed() =>
        Assert.Equal(PostRootStatus.Failed, RootStatusCalculator.Compute([T(TargetStatus.Failed)]));

    [Fact] public void Mixed_terminal_is_partial() =>
        Assert.Equal(PostRootStatus.PartiallyFailed, RootStatusCalculator.Compute([T(TargetStatus.Delivered), T(TargetStatus.Failed)]));

    [Fact] public void In_progress_delivering() =>
        Assert.Equal(PostRootStatus.Delivering, RootStatusCalculator.Compute([T(TargetStatus.Delivering), T(TargetStatus.Queued)]));

    [Fact] public void Generating_when_only_generating() =>
        Assert.Equal(PostRootStatus.Generating, RootStatusCalculator.Compute([T(TargetStatus.Ready), T(TargetStatus.Queued)]));

    [Fact] public void Queued_when_all_queued() =>
        Assert.Equal(PostRootStatus.Queued, RootStatusCalculator.Compute([T(TargetStatus.Queued)]));
}
