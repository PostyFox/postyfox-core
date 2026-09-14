using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Posting;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Worker.Posting.Tests.Support;
using Xunit;

namespace PostyFox.Worker.Posting.Tests;

/// <summary>
/// Exercises post automation (issue #323) end to end through the real pipeline wiring: delivery
/// setting a rule's due time, and <see cref="AutomationExecutionHandler"/> actually running it.
/// <see cref="PostTargetAutomationService.EnqueueDueAsync"/>'s due-time claim itself is covered
/// against a controllable clock in PostyFox.Infrastructure.Tests instead of here, where the harness's
/// real system clock makes exact timing awkward to assert.
/// </summary>
public class AutomationPipelineTests
{
    private static async Task CreatePostWithAutomationAsync(
        PipelineHarness h, string userId, Guid target, AutomationAction action, double delayHours)
    {
        using var scope = h.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<PostIntakeService>();
        var result = await intake.CreateAsync(userId, new CreatePostRequest(
            [target], "Title", "Hello", null, null, null, null, null, null,
            TargetAutomations: new Dictionary<Guid, IReadOnlyList<AutomationRequest>>
            {
                [target] = [new AutomationRequest(action, delayHours)]
            }));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Successful_delivery_sets_the_automations_due_time()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: true, supportsDelete: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");
        var before = DateTimeOffset.UtcNow;

        await CreatePostWithAutomationAsync(h, "u1", cid, AutomationAction.Delete, delayHours: 6);

        var automation = await h.InScopeAsync(db => db.PostTargetAutomations.FirstAsync());
        Assert.Equal(AutomationStatus.Pending, automation.Status);
        Assert.NotNull(automation.DueAt);
        // Delivered "now" (real clock) + 6h, generously bounded to absorb test execution time.
        Assert.InRange(automation.DueAt!.Value, before.AddHours(6).AddSeconds(-30), before.AddHours(6).AddMinutes(1));
    }

    [Fact]
    public async Task Failed_delivery_never_sets_a_due_time()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: false, supportsDelete: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");

        await CreatePostWithAutomationAsync(h, "u1", cid, AutomationAction.Delete, delayHours: 6);

        var automation = await h.InScopeAsync(db => db.PostTargetAutomations.FirstAsync());
        Assert.Null(automation.DueAt);
        Assert.Equal(AutomationStatus.Pending, automation.Status); // still waiting on a delivery that will never come
    }

    /// <summary>Seeds a delivered target with one due automation rule, bypassing intake/scheduling.</summary>
    private static async Task<(Post post, PostTarget target, PostTargetAutomation automation)> SeedDeliveredAsync(
        PipelineHarness h, AutomationAction action)
    {
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", RootStatus = PostRootStatus.Delivered };
        var target = new PostTarget
        {
            Id = Guid.NewGuid(), PostId = post.Id, Platform = "DiscordWH", Status = TargetStatus.Delivered, ExternalId = "ext-1"
        };
        var automation = new PostTargetAutomation
        {
            Id = Guid.NewGuid(), PostTargetId = target.Id, Action = action, DelayHours = 6,
            DueAt = DateTimeOffset.UtcNow.AddHours(-1), Status = AutomationStatus.Pending
        };
        await h.InScopeAsync(async db =>
        {
            db.Posts.Add(post);
            db.PostTargets.Add(target);
            db.PostTargetAutomations.Add(automation);
            await db.SaveChangesAsync();
            return true;
        });
        return (post, target, automation);
    }

    [Fact]
    public async Task Execution_handler_reposts_and_marks_the_rule_done()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: true, supportsRepost: true);
        using var h = new PipelineHarness(connector);
        var (post, target, automation) = await SeedDeliveredAsync(h, AutomationAction.Repost);

        using (var scope = h.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<ExecuteAutomationCommand>>();
            await handler.HandleAsync(new ExecuteAutomationCommand { PostId = post.Id, TargetId = target.Id, AutomationId = automation.Id }, default);
        }

        Assert.Equal(1, connector.RepostCalls);
        Assert.Equal("ext-1", connector.LastRepostExternalId);
        var reloaded = await h.InScopeAsync(db => db.PostTargetAutomations.AsNoTracking().FirstAsync());
        Assert.Equal(AutomationStatus.Done, reloaded.Status);
        Assert.NotNull(reloaded.ExecutedAt);
        Assert.Null(reloaded.Error);
    }

    [Fact]
    public async Task Execution_handler_deletes_and_marks_the_rule_done()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: true, supportsDelete: true);
        using var h = new PipelineHarness(connector);
        var (post, target, automation) = await SeedDeliveredAsync(h, AutomationAction.Delete);

        using (var scope = h.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<ExecuteAutomationCommand>>();
            await handler.HandleAsync(new ExecuteAutomationCommand { PostId = post.Id, TargetId = target.Id, AutomationId = automation.Id }, default);
        }

        Assert.Equal(1, connector.DeleteCalls);
        Assert.Equal("ext-1", connector.LastDeleteExternalId);
        var reloaded = await h.InScopeAsync(db => db.PostTargetAutomations.AsNoTracking().FirstAsync());
        Assert.Equal(AutomationStatus.Done, reloaded.Status);
    }

    [Fact]
    public async Task Execution_handler_marks_the_rule_failed_when_the_connector_reports_failure()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: false, supportsDelete: true);
        using var h = new PipelineHarness(connector);
        var (post, target, automation) = await SeedDeliveredAsync(h, AutomationAction.Delete);

        using (var scope = h.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<ExecuteAutomationCommand>>();
            await handler.HandleAsync(new ExecuteAutomationCommand { PostId = post.Id, TargetId = target.Id, AutomationId = automation.Id }, default);
        }

        var reloaded = await h.InScopeAsync(db => db.PostTargetAutomations.AsNoTracking().FirstAsync());
        Assert.Equal(AutomationStatus.Failed, reloaded.Status);
        Assert.NotNull(reloaded.Error);
        Assert.NotNull(reloaded.ExecutedAt);
    }

    [Fact]
    public async Task Execution_handler_fails_cleanly_when_the_connector_no_longer_supports_the_action()
    {
        // The connector's capability changed (or was never wired) after this rule was created:
        // defensive fallback, since intake normally rejects this before it can ever exist.
        var connector = new ProgrammableConnector("DiscordWH", succeed: true, supportsRepost: false);
        using var h = new PipelineHarness(connector);
        var (post, target, automation) = await SeedDeliveredAsync(h, AutomationAction.Repost);

        using (var scope = h.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<ExecuteAutomationCommand>>();
            await handler.HandleAsync(new ExecuteAutomationCommand { PostId = post.Id, TargetId = target.Id, AutomationId = automation.Id }, default);
        }

        Assert.Equal(0, connector.RepostCalls);
        var reloaded = await h.InScopeAsync(db => db.PostTargetAutomations.AsNoTracking().FirstAsync());
        Assert.Equal(AutomationStatus.Failed, reloaded.Status);
    }

    [Fact]
    public async Task Execution_handler_is_idempotent_for_an_already_cancelled_rule()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: true, supportsDelete: true);
        using var h = new PipelineHarness(connector);
        var (post, target, automation) = await SeedDeliveredAsync(h, AutomationAction.Delete);
        await h.InScopeAsync(async db =>
        {
            var row = await db.PostTargetAutomations.FirstAsync(a => a.Id == automation.Id);
            row.Status = AutomationStatus.Cancelled;
            await db.SaveChangesAsync();
            return true;
        });

        using (var scope = h.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<ExecuteAutomationCommand>>();
            await handler.HandleAsync(new ExecuteAutomationCommand { PostId = post.Id, TargetId = target.Id, AutomationId = automation.Id }, default);
        }

        Assert.Equal(0, connector.DeleteCalls); // never actually ran the cancelled rule
        var reloaded = await h.InScopeAsync(db => db.PostTargetAutomations.AsNoTracking().FirstAsync());
        Assert.Equal(AutomationStatus.Cancelled, reloaded.Status);
    }
}
