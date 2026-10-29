using OrderFlow.BuildingBlocks.Errors;
using Ordering.Api.Domain;

namespace OrderFlow.UnitTests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private static Order NewOrder() => new("user-1", "sara@example.com",
        [new OrderItem(Guid.NewGuid(), "Keyboard", 8_900, 2), new OrderItem(Guid.NewGuid(), "Mouse", 2_900, 1)], Now);

    [Fact]
    public void New_order_is_pending_with_the_total_of_its_lines()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal((2 * 8_900) + 2_900, order.TotalCents);
    }

    [Fact]
    public void Empty_orders_are_refused() =>
        Assert.Equal("empty_order", Assert.Throws<RuleException>(() => new Order("u", "e", [], Now)).Code);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Quantities_must_be_between_1_and_100(int quantity) =>
        Assert.Equal("invalid_quantity", Assert.Throws<RuleException>(() =>
            new Order("u", "e", [new OrderItem(Guid.NewGuid(), "X", 100, quantity)], Now)).Code);

    [Fact]
    public void Stock_events_move_a_pending_order_once()
    {
        var order = NewOrder();

        order.Confirm(Now);
        order.Reject("late duplicate", Now); // A second event for a decided order changes nothing.

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.StatusReason);
    }

    [Fact]
    public void Rejected_order_keeps_the_reason()
    {
        var order = NewOrder();
        order.Reject("Only 1 of Keyboard left.", Now);

        Assert.Equal(OrderStatus.Rejected, order.Status);
        Assert.Equal("Only 1 of Keyboard left.", order.StatusReason);
    }

    [Fact]
    public void Only_confirmed_orders_can_be_cancelled()
    {
        var pending = NewOrder();
        Assert.Equal("cannot_cancel", Assert.Throws<RuleException>(() => pending.Cancel("x", Now)).Code);

        var rejected = NewOrder();
        rejected.Reject("no stock", Now);
        Assert.Throws<RuleException>(() => rejected.Cancel("x", Now));

        var confirmed = NewOrder();
        confirmed.Confirm(Now);
        confirmed.Cancel("Cancelled by you.", Now);
        Assert.Equal(OrderStatus.Cancelled, confirmed.Status);
    }
}
