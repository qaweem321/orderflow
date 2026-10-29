namespace OrderFlow.Contracts;

/// <summary>An event one service publishes and others react to. <see cref="EventId"/> lets consumers ignore duplicates.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

public sealed record OrderLine(Guid ProductId, int Quantity);

/// <summary>Ordering: a customer placed an order. Catalog tries to reserve the stock.</summary>
public sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, IReadOnlyList<OrderLine> Lines) : IIntegrationEvent;

/// <summary>Catalog: every line of the order is reserved.</summary>
public sealed record StockReserved(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId) : IIntegrationEvent;

/// <summary>Catalog: the order could not be reserved; nothing was taken from stock.</summary>
public sealed record StockRejected(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, string Reason) : IIntegrationEvent;

/// <summary>Ordering: a confirmed order was cancelled. Catalog puts its stock back.</summary>
public sealed record OrderCancelled(Guid EventId, DateTimeOffset OccurredAt, Guid OrderId, IReadOnlyList<OrderLine> Lines) : IIntegrationEvent;
