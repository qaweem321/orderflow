using System.Text.Json;
using OrderFlow.Contracts;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>Event type names on the wire are the record names (<c>OrderPlaced</c>), also used as routing keys.</summary>
public static class EventSerializer
{
    private static readonly Dictionary<string, Type> Types = typeof(IIntegrationEvent).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IIntegrationEvent)))
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string NameOf(IIntegrationEvent @event) => @event.GetType().Name;

    public static string NameOf<T>() where T : IIntegrationEvent => typeof(T).Name;

    public static string Serialize(IIntegrationEvent @event) => JsonSerializer.Serialize(@event, @event.GetType(), Json);

    public static IIntegrationEvent Deserialize(string name, ReadOnlySpan<byte> body) =>
        Types.TryGetValue(name, out var type)
            ? (IIntegrationEvent)JsonSerializer.Deserialize(body, type, Json)!
            : throw new InvalidOperationException($"Unknown event type '{name}'.");
}
