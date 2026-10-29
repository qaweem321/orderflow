using System.ComponentModel.DataAnnotations;
using Ordering.Api.Domain;

namespace Ordering.Api.Features;

/// <summary>Quantity limits are checked by <see cref="Order"/> (nested list items aren't validated by attributes).</summary>
public sealed record PlaceOrderLine(Guid ProductId, int Quantity);

public sealed record PlaceOrderRequest([Required, MinLength(1), MaxLength(20)] PlaceOrderLine[] Lines);

public sealed record OrderLineDto(Guid ProductId, string ProductName, long UnitPriceCents, int Quantity, long LineTotalCents);

public sealed record OrderDto(
    Guid Id,
    string CustomerEmail,
    OrderStatus Status,
    string? StatusReason,
    long TotalCents,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDto From(Order o) => new(
        o.Id, o.CustomerEmail, o.Status, o.StatusReason, o.TotalCents, o.CreatedAt, o.UpdatedAt,
        o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.UnitPriceCents, l.Quantity, l.LineTotalCents)).ToList());
}

public sealed record OrderPage(IReadOnlyList<OrderDto> Items, int Total, int Page, int PageSize);
