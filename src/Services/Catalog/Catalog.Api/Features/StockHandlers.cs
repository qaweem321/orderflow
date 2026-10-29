using Catalog.Api.Data;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.BuildingBlocks.Persistence;
using OrderFlow.Contracts;

namespace Catalog.Api.Features;

/// <summary>Reserves every line of an order, or none of them, and tells Ordering which happened.</summary>
public sealed class OrderPlacedHandler(CatalogDbContext db, TimeProvider clock) : IHandles<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced message, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var products = (await db.LockProductsAsync(message.Lines.Select(l => l.ProductId), ct)).ToDictionary(p => p.Id);

        // Several lines may name the same product: check the total per product.
        var wanted = message.Lines.GroupBy(l => l.ProductId).Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity))).ToList();
        var problem = wanted
            .Select(w => products.TryGetValue(w.ProductId, out var p) ? p.CannotReserve(w.Quantity) : "A product in this order no longer exists.")
            .FirstOrDefault(reason => reason is not null);

        if (problem is not null)
        {
            db.Publish(new StockRejected(Guid.CreateVersion7(), now, message.OrderId, problem));
            return;
        }

        foreach (var (productId, quantity) in wanted)
        {
            products[productId].Reserve(quantity, now);
        }

        db.Publish(new StockReserved(Guid.CreateVersion7(), now, message.OrderId));
    }
}

/// <summary>Puts the stock of a cancelled order back.</summary>
public sealed class OrderCancelledHandler(CatalogDbContext db, TimeProvider clock) : IHandles<OrderCancelled>
{
    public async Task HandleAsync(OrderCancelled message, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var products = (await db.LockProductsAsync(message.Lines.Select(l => l.ProductId), ct)).ToDictionary(p => p.Id);
        foreach (var line in message.Lines.Where(l => products.ContainsKey(l.ProductId)))
        {
            products[line.ProductId].Release(line.Quantity, now);
        }
    }
}
