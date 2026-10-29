using System.Net;
using System.Net.Http.Json;
using System.Text;
using Catalog.Api.Data;
using Catalog.Api.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.BuildingBlocks.Persistence;
using OrderFlow.BuildingBlocks.Web;
using OrderFlow.Contracts;
using Ordering.Api.Domain;
using Ordering.Api.Features;

namespace OrderFlow.IntegrationTests;

/// <summary>End-to-end: HTTP into Ordering, events through RabbitMQ, stock in Catalog, events back.</summary>
public class OrderFlowTests(Backend stack)
{
    private HttpClient Admin => stack.Catalog.As(Roles.Admin);

    private async Task<ProductDto> CreateProductAsync(int stock, long priceCents = 1_000) =>
        await Backend.ReadAsync<ProductDto>(await Admin.PostAsJsonAsync("/api/catalog/products", new
        {
            sku = $"T-{Guid.NewGuid():N}"[..20],
            name = $"Test product {Guid.NewGuid():N}"[..24],
            description = "for tests",
            priceCents,
            stock,
        }));

    private async Task<ProductDto> GetProductAsync(Guid id) =>
        (await stack.Catalog.CreateClient().GetFromJsonAsync<ProductDto>($"/api/catalog/products/{id}", Backend.Json))!;

    private static async Task<OrderDto> PlaceAsync(HttpClient customer, params (Guid ProductId, int Quantity)[] lines) =>
        await Backend.ReadAsync<OrderDto>(await customer.PostAsJsonAsync("/api/orders",
            new { lines = lines.Select(l => new { productId = l.ProductId, quantity = l.Quantity }) }));

    private static Task<OrderDto> WaitForDecisionAsync(HttpClient customer, Guid orderId) =>
        Backend.EventuallyAsync(
            async () => (await customer.GetFromJsonAsync<OrderDto>($"/api/orders/{orderId}", Backend.Json))!,
            o => o.Status != OrderStatus.Pending);

    [Fact]
    public async Task Order_is_confirmed_and_stock_is_reserved()
    {
        var product = await CreateProductAsync(stock: 10, priceCents: 2_500);
        var customer = stack.Ordering.As(Roles.Customer);

        var order = await PlaceAsync(customer, (product.Id, 3));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(7_500, order.TotalCents);
        Assert.Equal(OrderStatus.Confirmed, (await WaitForDecisionAsync(customer, order.Id)).Status);
        Assert.Equal(7, (await GetProductAsync(product.Id)).Stock);
    }

    [Fact]
    public async Task Order_without_enough_stock_is_rejected_and_takes_nothing()
    {
        var plenty = await CreateProductAsync(stock: 10);
        var scarce = await CreateProductAsync(stock: 1);
        var customer = stack.Ordering.As(Roles.Customer);

        var order = await PlaceAsync(customer, (plenty.Id, 2), (scarce.Id, 2));
        var decided = await WaitForDecisionAsync(customer, order.Id);

        Assert.Equal(OrderStatus.Rejected, decided.Status);
        Assert.Equal($"Only 1 of {scarce.Name} left.", decided.StatusReason);
        Assert.Equal(10, (await GetProductAsync(plenty.Id)).Stock); // All or nothing.
        Assert.Equal(1, (await GetProductAsync(scarce.Id)).Stock);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_order_returns_the_stock()
    {
        var product = await CreateProductAsync(stock: 4);
        var customer = stack.Ordering.As(Roles.Customer);
        var order = await PlaceAsync(customer, (product.Id, 4));
        await WaitForDecisionAsync(customer, order.Id);

        var cancelled = await Backend.ReadAsync<OrderDto>(await customer.PostAsync($"/api/orders/{order.Id}/cancel", null));

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        var restocked = await Backend.EventuallyAsync(() => GetProductAsync(product.Id), p => p.Stock == 4);
        Assert.Equal(4, restocked.Stock);

        var again = await customer.PostAsync($"/api/orders/{order.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal("cannot_cancel", await Backend.ErrorCodeAsync(again));
    }

    [Fact]
    public async Task Last_units_go_to_exactly_as_many_orders_as_there_is_stock()
    {
        var product = await CreateProductAsync(stock: 5);
        var customers = Enumerable.Range(0, 12).Select(_ => stack.Ordering.As(Roles.Customer)).ToList();

        var orders = await Task.WhenAll(customers.Select(c => PlaceAsync(c, (product.Id, 1))));
        var decided = await Task.WhenAll(orders.Select((o, i) => WaitForDecisionAsync(customers[i], o.Id)));

        Assert.Equal(5, decided.Count(o => o.Status == OrderStatus.Confirmed));
        Assert.Equal(7, decided.Count(o => o.Status == OrderStatus.Rejected));
        Assert.Equal(0, (await GetProductAsync(product.Id)).Stock);
    }

    [Fact]
    public async Task Parallel_handlers_like_several_catalog_instances_never_oversell()
    {
        // One consumer handles messages one at a time; two Catalog instances would not. Call the handler in
        // parallel: row locks make them queue, so every call succeeds and exactly the stock is handed out.
        var product = await CreateProductAsync(stock: 5);
        var consumer = stack.Catalog.Services.GetRequiredService<EventConsumer<CatalogDbContext>>();
        var orderIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(orderIds.Select(id => consumer.HandleAsync(nameof(OrderPlaced),
            Encoding.UTF8.GetBytes(EventSerializer.Serialize(new OrderPlaced(Guid.CreateVersion7(), DateTimeOffset.UtcNow, id, [new OrderLine(product.Id, 1)]))),
            CancellationToken.None)));

        await using var scope = stack.Catalog.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var replies = await db.Set<OutboxMessage>().AsNoTracking().Where(m => m.Type == nameof(StockReserved) || m.Type == nameof(StockRejected)).ToListAsync();
        var mine = replies.Where(m => orderIds.Any(id => m.Payload.Contains(id.ToString(), StringComparison.Ordinal))).ToList();
        Assert.Equal(5, mine.Count(m => m.Type == nameof(StockReserved)));
        Assert.Equal(7, mine.Count(m => m.Type == nameof(StockRejected)));
        Assert.Equal(0, (await GetProductAsync(product.Id)).Stock);
    }

    [Fact]
    public async Task A_redelivered_event_is_handled_only_once()
    {
        var product = await CreateProductAsync(stock: 10);
        var placed = new OrderPlaced(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.NewGuid(), [new OrderLine(product.Id, 2)]);
        var body = Encoding.UTF8.GetBytes(EventSerializer.Serialize(placed));
        var consumer = stack.Catalog.Services.GetRequiredService<EventConsumer<CatalogDbContext>>();

        await consumer.HandleAsync(nameof(OrderPlaced), body, CancellationToken.None);
        await consumer.HandleAsync(nameof(OrderPlaced), body, CancellationToken.None);

        Assert.Equal(8, (await GetProductAsync(product.Id)).Stock);
    }

    [Fact]
    public async Task Customers_only_see_their_own_orders_and_admins_see_all()
    {
        var product = await CreateProductAsync(stock: 10);
        var alice = stack.Ordering.As(Roles.Customer);
        var bob = stack.Ordering.As(Roles.Customer);
        var order = await PlaceAsync(alice, (product.Id, 1));

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode);
        var bobsList = await bob.GetFromJsonAsync<OrderPage>("/api/orders?all=true", Backend.Json); // all=true is ignored for customers
        Assert.DoesNotContain(bobsList!.Items, o => o.Id == order.Id);

        var admin = stack.Ordering.As(Roles.Admin);
        var everything = await admin.GetFromJsonAsync<OrderPage>("/api/orders?all=true&pageSize=100", Backend.Json);
        Assert.Contains(everything!.Items, o => o.Id == order.Id);
    }

    [Fact]
    public async Task Ordering_unknown_or_inactive_products_is_refused()
    {
        var customer = stack.Ordering.As(Roles.Customer);

        var response = await customer.PostAsJsonAsync("/api/orders", new { lines = new[] { new { productId = Guid.NewGuid(), quantity = 1 } } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("unknown_product", await Backend.ErrorCodeAsync(response));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Quantities_are_validated(int quantity)
    {
        var product = await CreateProductAsync(stock: 10);
        var response = await stack.Ordering.As(Roles.Customer).PostAsJsonAsync("/api/orders",
            new { lines = new[] { new { productId = product.Id, quantity } } });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_quantity", await Backend.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Orders_need_a_signed_in_customer() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await stack.Ordering.CreateClient().GetAsync("/api/orders")).StatusCode);
}
