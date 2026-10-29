using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Contracts;

namespace OrderFlow.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>Adds the RabbitMQ connection, the outbox publisher and the queue consumer for <typeparamref name="TDbContext"/>.</summary>
    public static MessagingBuilder AddMessaging<TDbContext>(this IServiceCollection services) where TDbContext : DbContext
    {
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton(TimeProvider.System);
        var registry = new EventHandlerRegistry();
        services.AddSingleton(registry);
        services.AddSingleton<OutboxPublisher<TDbContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxPublisher<TDbContext>>());
        services.AddSingleton<EventConsumer<TDbContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<EventConsumer<TDbContext>>());
        return new MessagingBuilder(services, registry);
    }
}

public sealed class MessagingBuilder(IServiceCollection services, EventHandlerRegistry registry)
{
    public MessagingBuilder Handle<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : class, IHandles<TEvent>
    {
        services.AddScoped<THandler>();
        registry.Add<TEvent, THandler>();
        return this;
    }
}
