using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.Contracts;
using Ordering.Api.Data;

namespace Ordering.Api.Features;

public sealed class StockReservedHandler(OrderingDbContext db, TimeProvider clock) : IHandles<StockReserved>
{
    public async Task HandleAsync(StockReserved message, CancellationToken ct) =>
        (await db.Orders.SingleOrDefaultAsync(o => o.Id == message.OrderId, ct))?.Confirm(clock.GetUtcNow());
}

public sealed class StockRejectedHandler(OrderingDbContext db, TimeProvider clock) : IHandles<StockRejected>
{
    public async Task HandleAsync(StockRejected message, CancellationToken ct) =>
        (await db.Orders.SingleOrDefaultAsync(o => o.Id == message.OrderId, ct))?.Reject(message.Reason, clock.GetUtcNow());
}
