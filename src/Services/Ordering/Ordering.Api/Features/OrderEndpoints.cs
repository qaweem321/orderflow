using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Errors;
using OrderFlow.BuildingBlocks.Persistence;
using OrderFlow.BuildingBlocks.Web;
using OrderFlow.Contracts;
using Ordering.Api.Data;
using Ordering.Api.Domain;

namespace Ordering.Api.Features;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization(Roles.Customer);
        orders.MapPost("/", PlaceAsync).WithSummary("Place an order. It starts Pending until Catalog reserves the stock.");
        orders.MapGet("/", ListAsync).WithSummary("Your orders, newest first. Admins pass all=true to see every order.");
        orders.MapGet("/{id:guid}", GetAsync);
        orders.MapPost("/{id:guid}/cancel", CancelAsync).WithSummary("Cancel a confirmed order and return its stock.");
        return app;
    }

    private static async Task<IResult> PlaceAsync(
        PlaceOrderRequest request,
        ClaimsPrincipal user,
        CatalogClient catalog,
        OrderingDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        // Merge repeated products into one line each.
        var wanted = request.Lines.GroupBy(l => l.ProductId).Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity))).ToList();
        var products = await catalog.GetProductsAsync(wanted.Select(w => w.ProductId).ToList(), ct);

        var lines = wanted.Select(w => products.TryGetValue(w.ProductId, out var p) && p.IsActive
                ? new OrderItem(p.Id, p.Name, p.PriceCents, w.Quantity)
                : throw new RuleException("unknown_product", "One of the products is not for sale."))
            .ToList();

        var now = clock.GetUtcNow();
        var order = new Order(user.UserId(), user.FindFirstValue("email") ?? "", lines, now);
        db.Orders.Add(order); // Assigns the id, so the event below can carry it.
        db.Publish(new OrderPlaced(Guid.CreateVersion7(), now, order.Id, [.. lines.Select(l => new OrderLine(l.ProductId, l.Quantity))]));
        await db.SaveChangesAsync(ct); // Order and event commit together (outbox).

        return Results.Created($"/api/orders/{order.Id}", OrderDto.From(order));
    }

    private static async Task<OrderPage> ListAsync(
        ClaimsPrincipal user,
        OrderingDbContext db,
        bool all = false,
        OrderStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var everyone = all && user.IsInRole(Roles.Admin);
        var customerId = user.UserId();

        var query = db.Orders.AsNoTracking()
            .Where(o => everyone || o.CustomerId == customerId)
            .Where(o => status == null || o.Status == status);
        var total = await query.CountAsync(ct);
        var orders = await query.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new OrderPage(orders.Select(OrderDto.From).ToList(), total, page, pageSize);
    }

    private static async Task<OrderDto> GetAsync(Guid id, ClaimsPrincipal user, OrderingDbContext db, CancellationToken ct) =>
        OrderDto.From(await FindVisibleAsync(db, user, id, tracked: false, ct));

    private static async Task<OrderDto> CancelAsync(Guid id, ClaimsPrincipal user, OrderingDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var order = await FindVisibleAsync(db, user, id, tracked: true, ct);
        var now = clock.GetUtcNow();
        order.Cancel(user.IsInRole(Roles.Admin) && order.CustomerId != user.UserId() ? "Cancelled by the shop." : "Cancelled by you.", now);
        db.Publish(new OrderCancelled(Guid.CreateVersion7(), now, order.Id, [.. order.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity))]));
        await db.SaveChangesAsync(ct); // rowversion stops a double cancel from releasing stock twice.
        return OrderDto.From(order);
    }

    /// <summary>Customers see their own orders; admins see all. Others get 404, not 403, so ids don't leak.</summary>
    private static async Task<Order> FindVisibleAsync(OrderingDbContext db, ClaimsPrincipal user, Guid id, bool tracked, CancellationToken ct)
    {
        var customerId = user.UserId();
        var isAdmin = user.IsInRole(Roles.Admin);
        var query = tracked ? db.Orders : db.Orders.AsNoTracking();
        return await query.SingleOrDefaultAsync(o => o.Id == id && (isAdmin || o.CustomerId == customerId), ct)
            ?? throw new NotFoundException("Order");
    }
}
