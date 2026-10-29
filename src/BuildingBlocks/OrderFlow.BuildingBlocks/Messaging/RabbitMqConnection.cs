using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderFlow.BuildingBlocks.Messaging;

/// <summary>One long-lived connection per service (connections are expensive; channels are cheap).</summary>
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(options.Value.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    ClientProvidedName = options.Value.Queue,
                };
                _connection = await factory.CreateConnectionAsync(ct);
            }

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Declares the shared topic exchange (idempotent, safe to call from every service).</summary>
    public async Task DeclareExchangeAsync(IChannel channel, CancellationToken ct) =>
        await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
