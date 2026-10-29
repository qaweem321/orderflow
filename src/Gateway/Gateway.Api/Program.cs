using System.Threading.RateLimiting;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Identity;
using OrderFlow.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("OrderFlow Gateway (auth)");
builder.Services.AddValidation(); // Must be called here: the generator reads this project's request types.
builder.Services.AddSqlServerDb<IdentityDbContext>(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();

// One public entry point: YARP forwards /api/catalog/* and /api/orders/* to the services (routes in appsettings.json).
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var loginsPerMinute = builder.Configuration.GetValue("RateLimits:LoginsPerMinute", 10);
var requestsPerMinute = builder.Configuration.GetValue("RateLimits:RequestsPerMinute", 600);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c =>
        RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = requestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy(AuthEndpoints.LoginRateLimit, c =>
        RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
await app.MigrateAsync<IdentityDbContext>();
await DemoUsers.SeedAsync(app);

app.UseServiceDefaults();
app.UseRateLimiter();
app.MapServiceDefaults("OrderFlow Gateway (auth)");
app.MapAuthEndpoints();
app.MapReverseProxy();

await app.RunAsync();

