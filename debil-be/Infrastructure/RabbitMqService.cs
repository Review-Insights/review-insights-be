using System.Text;
using System.Text.Json;
using debil_be.Configuration;
using debil_be.Messaging;
using debil_be.Services;
using RabbitMQ.Client;

namespace debil_be.Infrastructure;

public class RabbitMqService : IQueueService, IHostedService
{
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public RabbitMqService(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_channel is not null) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_channel is not null) return;

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            _connection = await factory.CreateConnectionAsync(ct);
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            await _channel.ExchangeDeclareAsync(
                exchange: _settings.ExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                cancellationToken: ct);

            await _channel.QueueDeclareAsync(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);

            await _channel.QueueBindAsync(
                queue: _settings.QueueName,
                exchange: _settings.ExchangeName,
                routingKey: _settings.RoutingKey,
                cancellationToken: ct);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task PublishAnalysisRequestAsync(AnalysisRequestMessage message, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel!.BasicPublishAsync(
            exchange: _settings.ExchangeName,
            routingKey: _settings.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);
    }
}
