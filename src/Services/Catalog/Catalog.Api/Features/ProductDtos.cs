using System.ComponentModel.DataAnnotations;
using Catalog.Api.Domain;

namespace Catalog.Api.Features;

public sealed record ProductDto(Guid Id, string Sku, string Name, string Description, long PriceCents, int Stock, bool IsActive, DateTimeOffset UpdatedAt)
{
    public static ProductDto From(Product p) => new(p.Id, p.Sku, p.Name, p.Description, p.PriceCents, p.Stock, p.IsActive, p.UpdatedAt);
}

public sealed record ProductPage(IReadOnlyList<ProductDto> Items, int Total, int Page, int PageSize);

public sealed record CreateProductRequest(
    [Required, StringLength(40, MinimumLength = 2), RegularExpression("^[A-Za-z0-9-]+$")] string Sku,
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [StringLength(1000)] string? Description,
    [Range(1, 100_000_000)] long PriceCents,
    [Range(0, 1_000_000)] int Stock);

public sealed record UpdateProductRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [StringLength(1000)] string? Description,
    [Range(1, 100_000_000)] long PriceCents,
    bool IsActive);

public sealed record SetStockRequest([Range(0, 1_000_000)] int Stock);

public sealed record LookupRequest([Required, MinLength(1), MaxLength(100)] Guid[] Ids);
