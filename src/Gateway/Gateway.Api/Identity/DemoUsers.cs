using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderFlow.BuildingBlocks.Web;

namespace Gateway.Api.Identity;

/// <summary>Creates the README's demo logins on an empty database when <c>Seed:DemoUsers</c> is true.</summary>
public static class DemoUsers
{
    public static readonly (string Email, string Name, string Password, string Role)[] All =
    [
        ("admin@orderflow.test", "Shop Admin", "orderflow-admin", Roles.Admin),
        ("customer@orderflow.test", "Sara Customer", "orderflow-customer", Roles.Customer),
    ];

    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Seed:DemoUsers"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        foreach (var (email, name, password, role) in All)
        {
            var user = new User(email, name, role, TimeProvider.System.GetUtcNow());
            user.SetPasswordHash(hasher.HashPassword(user, password));
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
    }
}
