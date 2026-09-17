using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Services;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Periodically runs <see cref="ConnectorTokenRefreshService"/> to renew connector access tokens
/// ahead of expiry (currently just Instagram's long-lived token). Hosted in the posting worker,
/// same as <see cref="Persistence.PostRetentionSweeper"/>. Disabled when
/// <see cref="ConnectorRefreshOptions.Enabled"/> is false.
/// </summary>
public sealed class ConnectorTokenRefreshSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<ConnectorRefreshOptions> options,
    ILogger<ConnectorTokenRefreshSweeper> logger) : BackgroundService
{
    private readonly ConnectorRefreshOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Connector token refresh sweeper disabled by configuration.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.SweepIntervalHours));
        logger.LogInformation(
            "Connector token refresh sweeper started: refreshing within {Days} days of expiry, sweeping every {Hours}h.",
            _options.RefreshWithinDays, interval.TotalHours);

        // Run once at startup, then on the configured cadence.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ConnectorTokenRefreshService>();
                var refreshed = await service.RefreshDueAsync(stoppingToken);
                if (refreshed > 0)
                    logger.LogInformation("Connector token refresh sweep refreshed {Count} connector(s).", refreshed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Connector token refresh sweep failed; will retry next interval.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
