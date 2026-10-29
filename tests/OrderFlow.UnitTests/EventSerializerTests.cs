using System.Text;
using OrderFlow.BuildingBlocks.Messaging;
using OrderFlow.Contracts;

namespace OrderFlow.UnitTests;

public class EventSerializerTests
{
    [Fact]
    public void Events_round_trip_by_type_name()
    {
        var placed = new OrderPlaced(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, Guid.NewGuid(), [new OrderLine(Guid.NewGuid(), 3)]);

        var json = EventSerializer.Serialize(placed);
        var back = Assert.IsType<OrderPlaced>(EventSerializer.Deserialize(EventSerializer.NameOf(placed), Encoding.UTF8.GetBytes(json)));

        Assert.Equal("OrderPlaced", EventSerializer.NameOf(placed));
        Assert.Equal(placed.EventId, back.EventId);
        Assert.Equal(placed.Lines, back.Lines);
    }

    [Fact]
    public void Unknown_event_types_are_rejected() =>
        Assert.Throws<InvalidOperationException>(() => EventSerializer.Deserialize("Nope", "{}"u8));
}
