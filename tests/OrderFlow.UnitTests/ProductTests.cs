using Catalog.Api.Domain;
using OrderFlow.BuildingBlocks.Errors;

namespace OrderFlow.UnitTests;

public class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    private static Product Keyboard(int stock = 5) => new("kb-75", "Keyboard", "", 8_900, stock, Now);

    [Fact]
    public void Sku_is_stored_in_upper_case() => Assert.Equal("KB-75", Keyboard().Sku);

    [Fact]
    public void Reserve_takes_stock_and_release_puts_it_back()
    {
        var product = Keyboard(5);

        product.Reserve(3, Now);
        Assert.Equal(2, product.Stock);

        product.Release(3, Now);
        Assert.Equal(5, product.Stock);
    }

    [Theory]
    [InlineData(5, 6, "Only 5 of Keyboard left.")]
    [InlineData(0, 1, "Keyboard is out of stock.")]
    public void Reserving_more_than_stock_explains_why(int stock, int quantity, string reason)
    {
        var product = Keyboard(stock);

        Assert.Equal(reason, product.CannotReserve(quantity));
        Assert.Equal("insufficient_stock", Assert.Throws<RuleException>(() => product.Reserve(quantity, Now)).Code);
        Assert.Equal(stock, product.Stock);
    }

    [Fact]
    public void Inactive_products_cannot_be_reserved()
    {
        var product = Keyboard();
        product.Update("Keyboard", "", 8_900, isActive: false, Now);

        Assert.Equal("Keyboard is no longer sold.", product.CannotReserve(1));
    }

    [Fact]
    public void Price_and_stock_are_validated()
    {
        Assert.Equal("invalid_price", Assert.Throws<RuleException>(() => new Product("x-1", "X", "", 0, 1, Now)).Code);
        Assert.Equal("invalid_stock", Assert.Throws<RuleException>(() => Keyboard().SetStock(-1, Now)).Code);
    }
}
