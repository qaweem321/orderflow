using Microsoft.EntityFrameworkCore;

namespace Gateway.Api.Identity;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<User>(b =>
        {
            b.Property(u => u.Email).HasMaxLength(254);
            b.Property(u => u.DisplayName).HasMaxLength(80);
            b.Property(u => u.PasswordHash).HasMaxLength(200).IsUnicode(false);
            b.Property(u => u.Role).HasMaxLength(20).IsUnicode(false);
            b.HasIndex(u => u.Email).IsUnique();
        });
}
