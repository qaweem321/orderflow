using OrderFlow.BuildingBlocks.Errors;

namespace Ordering.Api.Domain;

public enum OrderStatus
{
    /// <summary>Placed; waiting for Catalog to reserve the stock.</summary>
    Pending,
    Confirmed,
    Rejected,
    Cancelled,
}

public sealed record OrderItem(Guid ProductId, string ProductName, long UnitPriceCents, int Quantity)
{
    public long LineTotalCents => UnitPriceCents * Quantity;
}

public class Order
{
    public const int MaxQuantityPerLine = 100;

    private readonly List<OrderItem> _lines = [];

    private Order() { }

    public Order(string customerId, string customerEmail, IEnumerable<OrderItem> lines, DateTimeOffset now)
    {
        _lines.AddRange(lines);
        if (_lines.Count == 0)
        {
            throw new RuleException("empty_order", "An order needs at least one item.");
        }

        if (_lines.Any(l => l.Quantity is < 1 or > MaxQuantityPerLine))
        {
            throw new RuleException("invalid_quantity", $"Each quantity must be between 1 and {MaxQuantityPerLine}.");
        }

        CustomerId = customerId;
        CustomerEmail = customerEmail;
        TotalCents = _lines.Sum(l => l.LineTotalCents);
        Status = OrderStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = "";
    public string CustomerEmail { get; private set; } = "";
    public OrderStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public long TotalCents { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyList<OrderItem> Lines => _lines;

    /// <summary>Stock arrived. Ignored unless still pending, so a duplicate event changes nothing.</summary>
    public void Confirm(DateTimeOffset now) => Move(OrderStatus.Pending, OrderStatus.Confirmed, null, now);

    public void Reject(string reason, DateTimeOffset now) => Move(OrderStatus.Pending, OrderStatus.Rejected, reason, now);

    /// <summary>
    /// Only confirmed orders can be cancelled: a pending order's stock may be reserved at any moment,
    /// and cancelling it then would leave that stock reserved forever.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status != OrderStatus.Confirmed)
        {
            throw new RuleException(
                "cannot_cancel",
                Status == OrderStatus.Pending
                    ? "This order is still being confirmed. Try again in a moment."
                    : $"A {Status.ToString().ToLowerInvariant()} order cannot be cancelled.");
        }

        Move(OrderStatus.Confirmed, OrderStatus.Cancelled, reason, now);
    }

    private void Move(OrderStatus from, OrderStatus to, string? reason, DateTimeOffset now)
    {
        if (Status != from)
        {
            return;
        }

        Status = to;
        StatusReason = reason;
        UpdatedAt = now;
    }
}
