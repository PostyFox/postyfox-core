using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Periodically runs <see cref="PostTargetAutomationService"/> to enqueue execution for post
/// automation rules (issue #323) whose <c>DueAt</c> has come due. Hosted in the posting worker,
/// alongside the queue consumers — the same shape as <see cref="PostSchedulerSweeper"/>, just polling
/// a coarser cadence since automation delays are author-chosen in hours, not seconds.
/// </summary>
public sealed class PostAutomationSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<PipelineOptions> options,
    ILogger<PostAutomationSweeper> logger) : BackgroundService
{
    private readonly PipelineOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.AutomationPollSeconds));
        logger.LogInformation("Post automation sweeper started: polling every {Seconds}s.", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Keep draining while a full batch comes back, so a large backlog clears in one pass
                // instead of trickling out one poll tick at a time.
                int enqueued;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PostTargetAutomationService>();
                    enqueued = await service.EnqueueDueAsync(stoppingToken);
                } while (enqueued >= _options.AutomationBatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Post automation sweep failed; will retry next interval.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
