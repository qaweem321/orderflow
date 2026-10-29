using System.Net.Http.Json;
using OrderFlow.BuildingBlocks.Errors;

namespace Ordering.Api.Features;

public sealed record CatalogProduct(Guid Id, string Name, long PriceCents, int Stock, bool IsActive);

/// <summary>
/// Synchronous call to Catalog for current names and prices: an order must show the price the customer
/// saw, so it is copied into the order. Retries, timeouts and a circuit breaker come from the resilience handler.
/// </summary>
public sealed class CatalogClient(HttpClient http)
{
    public async Task<IReadOnlyDictionary<Guid, CatalogProduct>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/catalog/products/lookup", new { ids }, ct);
            response.EnsureSuccessStatusCode();
            var products = await response.Content.ReadFromJsonAsync<List<CatalogProduct>>(ct) ?? [];
            return products.ToDictionary(p => p.Id);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
            or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            throw new UpstreamUnavailableException("The catalog is not reachable right now. Try again shortly.", ex);
        }
    }
}
