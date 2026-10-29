using System.Text.Json;
using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Persistence;

namespace Catalog.Api.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Loads products and locks their rows until commit, in id order so two orders can't deadlock.</summary>
    public Task<List<Product>> LockProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(ids.Distinct().Order());
        return Products
            .FromSql($"""
                SELECT p.* FROM Products p WITH (UPDLOCK, ROWLOCK)
                WHERE p.Id IN (SELECT CAST([value] AS uniqueidentifier) FROM OPENJSON({json}))
                """)
            .ToListAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(b =>
        {
            b.Property(p => p.Sku).HasMaxLength(40).IsUnicode(false);
            b.Property(p => p.Name).HasMaxLength(120);
            b.Property(p => p.Description).HasMaxLength(1000);
            b.Property(p => p.RowVersion).IsRowVersion();
            b.HasIndex(p => p.Sku).IsUnique();
            b.HasIndex(p => p.Name);
            b.ToTable(t => t.HasCheckConstraint("CK_Products_StockNotNegative", "[Stock] >= 0"));
        });
        modelBuilder.AddOutboxAndInbox();
    }
}
