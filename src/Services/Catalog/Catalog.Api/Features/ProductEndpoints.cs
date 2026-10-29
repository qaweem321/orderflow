using Catalog.Api.Data;
using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Errors;
using OrderFlow.BuildingBlocks.Web;

namespace Catalog.Api.Features;

public static class ProductEndpoints
{
    public const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/catalog/products").WithTags("Products");

        products.MapGet("/", ListAsync).AllowAnonymous()
            .WithSummary("Search products. Admins can include inactive ones.");
        products.MapGet("/{id:guid}", GetAsync).AllowAnonymous();
        products.MapPost("/lookup", LookupAsync).AllowAnonymous()
            .WithSummary("Fetch several products by id (used by Ordering for prices).");

        var admin = products.MapGroup("").RequireAuthorization(Roles.Admin);
        admin.MapPost("/", CreateAsync);
        admin.MapPut("/{id:guid}", UpdateAsync);
        admin.MapPut("/{id:guid}/stock", SetStockAsync).WithSummary("Set the available stock (restock or correction).");
        return app;
    }

    private static async Task<ProductPage> ListAsync(
        CatalogDbContext db,
        HttpContext http,
        string? search,
        bool includeInactive = false,
        int page = 1,
        int pageSize = 12,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var showInactive = includeInactive && http.User.IsInRole(Roles.Admin);

        var query = db.Products.AsNoTracking().Where(p => showInactive || p.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Sku.Contains(term));
        }

        // Count and page in parallel would need two DbContexts; two short indexed queries are fine here.
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => ProductDto.From(p))
            .ToListAsync(ct);
        return new ProductPage(items, total, page, pageSize);
    }

    private static async Task<ProductDto> GetAsync(Guid id, CatalogDbContext db, CancellationToken ct) =>
        ProductDto.From(await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product"));

    private static async Task<List<ProductDto>> LookupAsync(LookupRequest request, CatalogDbContext db, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => request.Ids.Contains(p.Id))
            .Select(p => ProductDto.From(p))
            .ToListAsync(ct);

    private static async Task<IResult> CreateAsync(CreateProductRequest request, CatalogDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var product = new Product(request.Sku, request.Name, request.Description ?? "", request.PriceCents, request.Stock, clock.GetUtcNow());
        db.Products.Add(product);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("sku_taken", $"A product with SKU {product.Sku} already exists.");
        }

        return Results.Created($"/api/catalog/products/{product.Id}", ProductDto.From(product));
    }

    private static async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CatalogDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var product = await FindAsync(db, id, ct);
        product.Update(request.Name, request.Description ?? "", request.PriceCents, request.IsActive, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return ProductDto.From(product);
    }

    private static async Task<ProductDto> SetStockAsync(Guid id, SetStockRequest request, CatalogDbContext db, TimeProvider clock, CancellationToken ct)
    {
        // rowversion: if an order reserved stock since this product was loaded, the save fails with 409.
        var product = await FindAsync(db, id, ct);
        product.SetStock(request.Stock, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return ProductDto.From(product);
    }

    private static async Task<Product> FindAsync(CatalogDbContext db, Guid id, CancellationToken ct) =>
        await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Product");
}
