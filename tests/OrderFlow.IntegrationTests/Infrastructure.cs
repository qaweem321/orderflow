using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catalog.Api.Data;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.BuildingBlocks.Web;
using Ordering.Api.Data;
using Ordering.Api.Features;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(OrderFlow.IntegrationTests.Backend))]

namespace OrderFlow.IntegrationTests;

/// <summary>
/// The whole back end for the test run: SQL Server and RabbitMQ in containers, Catalog, Ordering and the
/// gateway in memory. Ordering calls Catalog through Catalog's in-memory test server.
/// </summary>
public sealed class Backend : IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-at-least-32-chars";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4.3.6-management-alpine").Build();

    public ServiceFactory<CatalogDbContext> Catalog { get; private set; } = null!;
    public ServiceFactory<OrderingDbContext> Ordering { get; private set; } = null!;
    public ServiceFactory<IdentityDbContext> Gateway { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _rabbit.StartAsync());

        Catalog = new ServiceFactory<CatalogDbContext>(Settings("OrderFlowCatalog", "catalog"));
        Gateway = new ServiceFactory<IdentityDbContext>(Settings("OrderFlowIdentity", "gateway", ("Seed:DemoUsers", "true"), ("RateLimits:LoginsPerMinute", "1000")));
        _ = Catalog.Services; // Start Catalog first: Ordering's client needs its test server.
        Ordering = new ServiceFactory<OrderingDbContext>(
            Settings("OrderFlowOrdering", "ordering", ("Services:Catalog", "http://catalog")),
            services => services.AddHttpClient<CatalogClient>().ConfigurePrimaryHttpMessageHandler(() => Catalog.Server.CreateHandler()));
        _ = Ordering.Services;
        _ = Gateway.Services;
    }

    public async ValueTask DisposeAsync()
    {
        await Ordering.DisposeAsync();
        await Catalog.DisposeAsync();
        await Gateway.DisposeAsync();
        await Task.WhenAll(_sql.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    private Dictionary<string, string> Settings(string database, string queue, params (string Key, string Value)[] extra)
    {
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings:Default"] = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = database }.ConnectionString,
            ["Database:MigrateOnStartup"] = "true",
            ["Jwt:SigningKey"] = SigningKey,
            ["RabbitMq:ConnectionString"] = _rabbit.GetConnectionString(),
            ["RabbitMq:Queue"] = queue,
            ["RabbitMq:OutboxPollInterval"] = "00:00:00.100",
        };
        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return settings;
    }

    /// <summary>A client with a token for a made-up user, signed with the shared key (as the gateway would).</summary>
    public static HttpClient Authorize(HttpClient client, string role, string? userId = null)
    {
        var id = userId ?? Guid.NewGuid().ToString();
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "orderflow",
            Audience = "orderflow",
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity([new Claim("sub", id), new Claim("email", $"{id[..8]}@example.com"), new Claim("role", role)]),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var code) ? code.GetString() : null;

    /// <summary>Polls until <paramref name="done"/> holds: events travel through RabbitMQ asynchronously.</summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> done, int seconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            var value = await read();
            if (done(value) || DateTime.UtcNow > deadline)
            {
                return value;
            }

            await Task.Delay(100);
        }
    }
}

/// <summary>Hosts one service in memory. Any type from the service's assembly can be the entry point.</summary>
public sealed class ServiceFactory<TEntry>(Dictionary<string, string> settings, Action<IServiceCollection>? configure = null)
    : WebApplicationFactory<TEntry> where TEntry : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        if (configure is not null)
        {
            builder.ConfigureTestServices(configure);
        }
    }

    public HttpClient As(string role, string? userId = null) => Backend.Authorize(CreateClient(), role, userId);
}
