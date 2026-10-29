using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.Contracts;

namespace OrderFlow.BuildingBlocks.Persistence;

/// <summary>An event saved in the same transaction as the change it describes, published later (transactional outbox).</summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(IIntegrationEvent @event)
    {
        Id = @event.EventId;
        Type = EventSerializer.NameOf(@event);
        Payload = EventSerializer.Serialize(@event);
        OccurredAt = @event.OccurredAt;
        NextAttemptAt = @event.OccurredAt;
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = "";
    public string Payload { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
}

/// <summary>Remembers which events a consumer already handled, so a redelivered message has no effect.</summary>
public sealed class InboxMessage
{
    private InboxMessage() { }

    public InboxMessage(Guid eventId, string type, DateTimeOffset handledAt)
    {
        EventId = eventId;
        Type = type;
        HandledAt = handledAt;
    }

    public Guid EventId { get; private set; }
    public string Type { get; private set; } = "";
    public DateTimeOffset HandledAt { get; private set; }
}

public static class MessagingModel
{
    /// <summary>Adds the OutboxMessages and InboxMessages tables to a service's model.</summary>
    public static ModelBuilder AddOutboxAndInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages"); // Named explicitly: the publisher's raw SQL uses this name.
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(m => m.Type).HasMaxLength(100);
            b.Property(m => m.Payload).HasMaxLength(-1);
            b.Property(m => m.LastError).HasMaxLength(500);
            b.HasIndex(m => m.NextAttemptAt).HasFilter("[PublishedAt] IS NULL");
        });
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            b.HasKey(m => m.EventId);
            b.Property(m => m.Type).HasMaxLength(100);
        });
        return modelBuilder;
    }

    /// <summary>Queues an event to publish when the current transaction commits.</summary>
    public static void Publish(this DbContext db, IIntegrationEvent @event) => db.Set<OutboxMessage>().Add(new OutboxMessage(@event));
}
