using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Periodically runs <see cref="PostSchedulerService"/> to enqueue generation for scheduled posts
/// whose <c>PostAt</c> has come due. Hosted in the posting worker, alongside the queue consumers.
/// </summary>
public sealed class PostSchedulerSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<PipelineOptions> options,
    ILogger<PostSchedulerSweeper> logger) : BackgroundService
{
    private readonly PipelineOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.SchedulerPollSeconds));
        logger.LogInformation("Post scheduler started: polling every {Seconds}s.", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Keep draining while a full batch comes back, so a large backlog (e.g. many posts
                // scheduled for the same moment) clears in one pass instead of trickling out one
                // poll tick at a time.
                int enqueued;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PostSchedulerService>();
                    enqueued = await service.EnqueueDueAsync(stoppingToken);
                } while (enqueued >= _options.SchedulerBatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Post scheduler pass failed; will retry next interval.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
