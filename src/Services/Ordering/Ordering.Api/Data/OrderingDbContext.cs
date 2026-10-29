using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Persistence;
using Ordering.Api.Domain;

namespace Ordering.Api.Data;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.Property(o => o.CustomerId).HasMaxLength(64).IsUnicode(false);
            b.Property(o => o.CustomerEmail).HasMaxLength(254);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(o => o.StatusReason).HasMaxLength(300);
            b.Property(o => o.RowVersion).IsRowVersion();
            b.HasIndex(o => new { o.CustomerId, o.CreatedAt });
            b.HasIndex(o => o.CreatedAt);
            b.OwnsMany(o => o.Lines, l =>
            {
                l.ToTable("OrderLines");
                l.WithOwner().HasForeignKey("OrderId");
                l.Property<int>("Id");
                l.HasKey("Id");
                l.Property(x => x.ProductName).HasMaxLength(120);
                l.Ignore(x => x.LineTotalCents);
            });
            b.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        modelBuilder.AddOutboxAndInbox();
    }
}
