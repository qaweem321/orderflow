using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.BuildingBlocks.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Consumes this service's queue. Each message is handled in one database transaction together with
/// an inbox row keyed by EventId, so a redelivered message is skipped and the handler's own outbox
/// events commit with it. A message that fails twice goes to the "&lt;queue&gt;.dead" queue for inspection.
/// </summary>
public sealed partial class EventConsumer<TDbContext>(
    IServiceScopeFactory scopes,
    RabbitMqConnection rabbit,
    EventHandlerRegistry registry,
    TimeProvider clock,
    IOptions<RabbitMqOptions> options,
    ILogger<EventConsumer<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry until the broker is reachable: in Docker the service may start before RabbitMQ is ready.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogStartFailed(logger, ex);
                await Task.Delay(TimeSpan.FromSeconds(3), clock, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var queue = options.Value.Queue;
        var connection = await rabbit.GetAsync(ct);
        _channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await _channel.BasicQosAsync(0, prefetchCount: 10, global: false, ct);
        await rabbit.DeclareExchangeAsync(_channel, ct);

        await _channel.QueueDeclareAsync($"{queue}.dead", durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = $"{queue}.dead",
            },
            cancellationToken: ct);
        foreach (var name in registry.EventNames)
        {
            await _channel.QueueBindAsync(queue, options.Value.Exchange, routingKey: name, cancellationToken: ct);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, delivery) => OnMessageAsync(_channel, delivery, ct);
        await _channel.BasicConsumeAsync(queue, autoAck: false, consumer, ct);
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken ct)
    {
        try
        {
            await HandleAsync(delivery.BasicProperties.Type ?? "", delivery.Body, ct);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // First failure: try once more (may be a passing database hiccup). Second: dead-letter it.
            LogHandleFailed(logger, delivery.BasicProperties.MessageId, delivery.Redelivered, ex);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: !delivery.Redelivered, ct);
        }
    }

    /// <summary>Handles one message exactly once per EventId. Public so tests can drive it without a broker.</summary>
    public async Task HandleAsync(string type, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (!registry.TryGet(type, out var handler))
        {
            throw new InvalidOperationException($"No handler for event type '{type}'.");
        }

        var @event = EventSerializer.Deserialize(type, body.Span);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Set<InboxMessage>().AnyAsync(m => m.EventId == @event.EventId, ct))
        {
            return; // Already handled: a redelivery after a lost ack.
        }

        db.Set<InboxMessage>().Add(new InboxMessage(@event.EventId, type, clock.GetUtcNow()));
        await handler(scope.ServiceProvider, @event, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }
            && ex.Entries.Any(e => e.Entity is InboxMessage))
        {
            return; // Another instance handled the same delivery at the same moment.
        }

        await tx.CommitAsync(ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not start consuming; retrying")]
    private static partial void LogStartFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handling message {MessageId} failed (redelivered: {Redelivered})")]
    private static partial void LogHandleFailed(ILogger logger, string? messageId, bool redelivered, Exception exception);
}
