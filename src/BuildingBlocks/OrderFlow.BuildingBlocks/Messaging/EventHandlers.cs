using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Contracts;

namespace OrderFlow.BuildingBlocks.Messaging;

public interface IHandles<in TEvent> where TEvent : IIntegrationEvent
{
    /// <summary>Runs inside the consumer's database transaction; save nothing yourself, the consumer commits.</summary>
    Task HandleAsync(TEvent message, CancellationToken ct);
}

/// <summary>Which event names this service subscribes to, and how to dispatch each one.</summary>
public sealed class EventHandlerRegistry
{
    private readonly Dictionary<string, Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task>> _handlers = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> EventNames => _handlers.Keys;

    public void Add<TEvent, THandler>()
        where TEvent : IIntegrationEvent
        where THandler : IHandles<TEvent> =>
        _handlers[EventSerializer.NameOf<TEvent>()] = (services, @event, ct) =>
            services.GetRequiredService<THandler>().HandleAsync((TEvent)@event, ct);

    public bool TryGet(string name, out Func<IServiceProvider, IIntegrationEvent, CancellationToken, Task> handler) =>
        _handlers.TryGetValue(name, out handler!);
}
