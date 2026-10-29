using Catalog.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

/// <summary>Adds a small demo catalog to an empty database when <c>Seed:DemoData</c> is true.</summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Seed:DemoData"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        if (await db.Products.AnyAsync())
        {
            return;
        }

        var now = TimeProvider.System.GetUtcNow();
        db.Products.AddRange(
            new Product("KB-75", "Mechanical keyboard", "75% layout, hot-swappable switches, USB-C.", 8_900, 25, now),
            new Product("MS-01", "Wireless mouse", "Silent clicks, 70-day battery.", 2_900, 60, now),
            new Product("HD-07", "USB-C hub", "7 ports: HDMI 4K, 3x USB-A, SD, microSD, 100 W pass-through.", 4_500, 40, now),
            new Product("MN-27", "27-inch monitor", "QHD IPS, 75 Hz, height-adjustable stand.", 24_900, 8, now),
            new Product("WC-HD", "1080p webcam", "Autofocus, dual microphones, privacy shutter.", 5_900, 15, now),
            new Product("HS-ANC", "Noise-cancelling headset", "Over-ear, 30 h battery, boom mic.", 12_900, 3, now),
            new Product("DM-XL", "Desk mat XL", "90 x 40 cm, stitched edges.", 1_900, 100, now),
            new Product("LS-AL", "Laptop stand", "Aluminium, six heights, folds flat.", 3_400, 0, now));
        await db.SaveChangesAsync();
    }
}
