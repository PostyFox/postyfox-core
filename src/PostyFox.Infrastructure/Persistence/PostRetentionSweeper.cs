using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;

namespace PostyFox.Infrastructure.Persistence;

/// <summary>
/// Periodically runs <see cref="PostRetentionService"/> to hard-delete posts past the retention
/// window. Hosted in the posting worker. Disabled when <see cref="RetentionOptions.Enabled"/> is false.
/// </summary>
public sealed class PostRetentionSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<PostRetentionSweeper> logger) : BackgroundService
{
    private readonly RetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Post retention sweeper disabled by configuration.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.SweepIntervalHours));
        logger.LogInformation(
            "Post retention sweeper started: keeping {Days} days, sweeping every {Hours}h.",
            _options.PostRetentionDays, interval.TotalHours);

        // Run once at startup, then on the configured cadence.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Keep sweeping while a full batch comes back, so a large backlog drains in one pass.
                int deleted;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<PostRetentionService>();
                    deleted = await service.PurgeAsync(stoppingToken);
                } while (deleted >= _options.SweepBatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Post retention sweep failed; will retry next interval.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
