using Microsoft.Extensions.Options;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.BuildingBlocks.Web;
using OrderFlow.Contracts;
using Ordering.Api.Data;
using Ordering.Api.Features;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults("OrderFlow Ordering API");
builder.Services.AddValidation(); // Must be called here: the generator reads this project's request types.
builder.Services.AddSqlServerDb<OrderingDbContext>(builder.Configuration);
builder.Services.AddMessaging<OrderingDbContext>()
    .Handle<StockReserved, StockReservedHandler>()
    .Handle<StockRejected, StockRejectedHandler>();

builder.Services.AddOptions<ServiceUrls>().BindConfiguration(ServiceUrls.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHttpClient<CatalogClient>((sp, http) =>
        http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ServiceUrls>>().Value.Catalog))
    .AddStandardResilienceHandler();

var app = builder.Build();
await app.MigrateAsync<OrderingDbContext>();

app.UseServiceDefaults();
app.MapServiceDefaults("OrderFlow Ordering API");
app.MapOrderEndpoints();

await app.RunAsync();

