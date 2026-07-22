using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using PostyFox.Application;
using PostyFox.Application.Telemetry;
using PostyFox.Application.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PostyFox.Infrastructure.Messaging;

/// <summary>
/// Hosted consumer that binds a queue for <typeparamref name="T"/> and dispatches each message
/// to the scoped <see cref="IMessageHandler{T}"/>. Unhandled exceptions dead-letter the message
/// (per-target delivery retries are handled in the pipeline handler via delayed re-publish).
/// </summary>
public sealed class RabbitMqSubscriber<T>(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqSubscriber<T>> logger) : BackgroundService where T : class
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var exchange = connection.Options.Exchange;
        var queue = QueueNames.For<T>();

        var conn = await connection.GetAsync(stoppingToken);
        _channel = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await Topology.DeclareExchangeAsync(_channel, exchange, stoppingToken);
        await Topology.DeclareQueueAsync(_channel, exchange, queue, stoppingToken);
        await _channel.BasicQosAsync(0, connection.Options.Prefetch, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            // Continue the producer's trace: extract the context the publisher injected and start a
            // consumer span parented to it. All handler logs (and any HttpClient spans it makes)
            // inherit this span's traceId, so the work links back to the originating API request.
            var parent = MessagingTelemetry.Extract(ea.BasicProperties);
            Baggage.Current = parent.Baggage;
            using var activity = MessagingTelemetry.Source.StartActivity(
                $"{queue} receive", ActivityKind.Consumer, parent.ActivityContext);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", queue);
            activity?.SetTag("messaging.operation", "receive");

            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var message = Json.Deserialize<T>(json)
                    ?? throw new InvalidOperationException($"Null message for {queue}");

                // Stamp the business keys onto the span + ambient Baggage so every log emitted while
                // handling this message (incl. framework/HttpClient logs) is searchable by PostId.
                if (message is ITraceableMessage tm)
                {
                    MessagingTelemetry.TagSpan(activity, tm.PostId, tm.TargetId);
                    PostTelemetry.SetBusinessBaggage(tm.PostId, tm.TargetId);
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();
                await handler.HandleAsync(message, stoppingToken);

                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Handler for {Queue} failed; dead-lettering message", queue);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(queue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Subscribed to queue {Queue}", queue);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
