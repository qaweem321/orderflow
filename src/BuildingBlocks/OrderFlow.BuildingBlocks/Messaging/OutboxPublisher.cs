using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.BuildingBlocks.Persistence;
using RabbitMQ.Client;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>
/// Publishes outbox rows to RabbitMQ with publisher confirms, so a row is marked published only after
/// the broker has stored it. Several instances can run: each claims rows with READPAST, and the claim
/// pushes NextAttemptAt forward as a lease. Delivery is at-least-once; consumers de-duplicate by EventId.
/// </summary>
public sealed partial class OutboxPublisher<TDbContext>(
    IServiceScopeFactory scopes,
    RabbitMqConnection rabbit,
    TimeProvider clock,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxPublisher<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.OutboxPollInterval, clock);
        do
        {
            try
            {
                while (await PublishBatchAsync(stoppingToken) == BatchSize)
                {
                    // More are waiting: keep going without sleeping.
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBatchFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> PublishBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var now = clock.GetUtcNow();

        var batch = await db.Set<OutboxMessage>()
            .FromSql($"""
                UPDATE TOP ({BatchSize}) o
                SET o.NextAttemptAt = {now + Lease}
                OUTPUT inserted.*
                FROM OutboxMessages o WITH (READPAST, UPDLOCK, ROWLOCK)
                WHERE o.PublishedAt IS NULL AND o.NextAttemptAt <= {now}
                """)
            .AsNoTracking()
            .ToListAsync(ct);
        if (batch.Count == 0)
        {
            return 0;
        }

        var connection = await rabbit.GetAsync(ct);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await rabbit.DeclareExchangeAsync(channel, ct);

        // Publish in order (events of one order must not overtake each other); confirms are awaited per message.
        var published = new List<Guid>();
        foreach (var message in batch.OrderBy(m => m.OccurredAt))
        {
            try
            {
                var properties = new BasicProperties
                {
                    MessageId = message.Id.ToString(),
                    Type = message.Type,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
                };
                await channel.BasicPublishAsync(options.Value.Exchange, message.Type, mandatory: false, properties,
                    System.Text.Encoding.UTF8.GetBytes(message.Payload), ct);
                published.Add(message.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPublishFailed(logger, message.Id, ex);
                var error = ex.Message[..Math.Min(ex.Message.Length, 500)];
                await db.Set<OutboxMessage>().Where(m => m.Id == message.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.Attempts, m => m.Attempts + 1)
                    .SetProperty(m => m.LastError, error), ct);
                break; // Keep order: the rest of the batch waits for the lease to expire.
            }
        }

        if (published.Count > 0)
        {
            var at = clock.GetUtcNow();
            await db.Set<OutboxMessage>().Where(m => published.Contains(m.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.PublishedAt, at), ct);
        }

        return batch.Count;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing outbox message {MessageId} failed")]
    private static partial void LogPublishFailed(ILogger logger, Guid messageId, Exception exception);
}
