using Catalog.Api.Data;
using Catalog.Api.Features;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.BuildingBlocks.Web;
using OrderFlow.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("OrderFlow Catalog API");
builder.Services.AddValidation(); // Must be called here: the generator reads this project's request types.
builder.Services.AddSqlServerDb<CatalogDbContext>(builder.Configuration);
builder.Services.AddMessaging<CatalogDbContext>()
    .Handle<OrderPlaced, OrderPlacedHandler>()
    .Handle<OrderCancelled, OrderCancelledHandler>();

var app = builder.Build();
await app.MigrateAsync<CatalogDbContext>();
await DemoSeeder.SeedAsync(app);

app.UseServiceDefaults();
app.MapServiceDefaults("OrderFlow Catalog API");
app.MapProductEndpoints();

await app.RunAsync();

