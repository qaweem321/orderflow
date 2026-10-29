using System.Net;
using System.Net.Http.Json;
using Catalog.Api.Features;
using OrderFlow.BuildingBlocks.Web;

namespace OrderFlow.IntegrationTests;

public class CatalogTests(Backend stack)
{
    private static object NewProduct(string? sku = null) => new
    {
        sku = sku ?? $"C-{Guid.NewGuid():N}"[..16],
        name = "Catalog test item",
        description = "",
        priceCents = 1_500,
        stock = 3,
    };

    [Fact]
    public async Task Only_admins_can_change_the_catalog()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await stack.Catalog.CreateClient().PostAsJsonAsync("/api/catalog/products", NewProduct())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stack.Catalog.As(Roles.Customer).PostAsJsonAsync("/api/catalog/products", NewProduct())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await stack.Catalog.As(Roles.Admin).PostAsJsonAsync("/api/catalog/products", NewProduct())).StatusCode);
    }

    [Fact]
    public async Task Sku_must_be_unique_ignoring_case()
    {
        var admin = stack.Catalog.As(Roles.Admin);
        var sku = $"U-{Guid.NewGuid():N}"[..12];
        (await admin.PostAsJsonAsync("/api/catalog/products", NewProduct(sku))).EnsureSuccessStatusCode();

        var duplicate = await admin.PostAsJsonAsync("/api/catalog/products", NewProduct(sku.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("sku_taken", await Backend.ErrorCodeAsync(duplicate));
    }

    [Fact]
    public async Task Invalid_products_are_rejected() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await stack.Catalog.As(Roles.Admin).PostAsJsonAsync("/api/catalog/products",
            new { sku = "has spaces", name = "x", priceCents = 0, stock = -1 })).StatusCode);

    [Fact]
    public async Task Inactive_products_are_hidden_from_shoppers()
    {
        var admin = stack.Catalog.As(Roles.Admin);
        var product = await Backend.ReadAsync<ProductDto>(await admin.PostAsJsonAsync("/api/catalog/products", NewProduct()));
        (await admin.PutAsJsonAsync($"/api/catalog/products/{product.Id}",
            new { name = product.Name, description = "", priceCents = product.PriceCents, isActive = false })).EnsureSuccessStatusCode();

        var shopperView = await stack.Catalog.CreateClient().GetFromJsonAsync<ProductPage>($"/api/catalog/products?search={product.Sku}&includeInactive=true", Backend.Json);
        var adminView = await admin.GetFromJsonAsync<ProductPage>($"/api/catalog/products?search={product.Sku}&includeInactive=true", Backend.Json);

        Assert.Empty(shopperView!.Items);
        Assert.Single(adminView!.Items);
    }
}
