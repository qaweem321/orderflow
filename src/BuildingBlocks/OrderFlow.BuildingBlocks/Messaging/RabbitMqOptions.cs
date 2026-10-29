using System.ComponentModel.DataAnnotations;

namespace OrderFlow.BuildingBlocks.Messaging;

public sealed class RabbitMqOptions
{
    public const string Section = "RabbitMq";

    /// <summary>For example <c>amqp://guest:guest@localhost:5672</c>.</summary>
    [Required]
    public string ConnectionString { get; set; } = "";

    /// <summary>The topic exchange all services publish to. Routing key = event type name.</summary>
    [Required]
    public string Exchange { get; set; } = "orderflow.events";

    /// <summary>This service's queue; each service gets its own copy of the events it subscribes to.</summary>
    [Required]
    public string Queue { get; set; } = "";

    [Range(typeof(TimeSpan), "00:00:00.050", "00:01:00")]
    public TimeSpan OutboxPollInterval { get; set; } = TimeSpan.FromSeconds(1);
}
