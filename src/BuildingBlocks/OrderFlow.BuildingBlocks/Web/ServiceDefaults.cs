using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OrderFlow.BuildingBlocks.Errors;
using Scalar.AspNetCore;

namespace OrderFlow.BuildingBlocks.Web;

/// <summary>What every OrderFlow service has in common, so each Program.cs only holds its own parts.</summary>
public static class ServiceDefaults
{
    /// <remarks>
    /// Each service must call <c>builder.Services.AddValidation()</c> in its own Program.cs: the validation source
    /// generator only sees request types of the project that makes the call.
    /// </remarks>
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder, string title)
    {
        var services = builder.Services;
        services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
            c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<AppExceptionHandler>();
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddHealthChecks();

        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.Key(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "email",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Roles.Customer, p => p.RequireRole(Roles.Customer, Roles.Admin));

        // Behind a reverse proxy, trust X-Forwarded-For only from its network, and only the last hop.
        var proxies = builder.Configuration.GetSection("ReverseProxyNetworks").Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
            foreach (var network in proxies)
            {
                o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        services.AddOpenApi(o => o.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo { Title = title, Version = "v1" };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token from POST /api/auth/login on the gateway.",
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }));
        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            await next();
        });
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static WebApplication MapServiceDefaults(this WebApplication app, string docsTitle)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        app.MapOpenApi();
        app.MapScalarApiReference("/docs", o => o.WithTitle(docsTitle));
        return app;
    }

    /// <summary>Adds a SQL Server DbContext, its readiness check, and fails at start-up without a connection string.</summary>
    public static IServiceCollection AddSqlServerDb<TDbContext>(this IServiceCollection services, IConfiguration configuration)
        where TDbContext : DbContext
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not set. See .env.example.");
        }

        services.AddDbContext<TDbContext>(o => o.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<TDbContext>("database", tags: ["ready"]);
        return services;
    }

    public static async Task MigrateAsync<TDbContext>(this WebApplication app) where TDbContext : DbContext
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TDbContext>().Database.MigrateAsync();
    }

    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? throw new InvalidOperationException("Token has no subject.");
}
