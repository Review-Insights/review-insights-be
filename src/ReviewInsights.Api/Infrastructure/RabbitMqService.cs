using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using ReviewInsights.Api.Configuration;
using ReviewInsights.Api.Messaging;

namespace ReviewInsights.Api.Infrastructure;

public class RabbitMqService : IQueueService, IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public RabbitMqService(RabbitMqSettings settings, ILogger<RabbitMqService> logger)
    {
        _settings = settings;
        _logger = logger;
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

            await DeclareAndBindAsync(_settings.AnalyzeReviewsQueue, _settings.AnalyzeReviewsRoutingKey, ct);
            await DeclareAndBindAsync(_settings.GenerateReportQueue, _settings.GenerateReportRoutingKey, ct);

            await DeclareAndBindAsync(_settings.UploadResultsQueue, _settings.UploadResultsRoutingKey, ct);
            await DeclareAndBindAsync(_settings.UploadErrorQueue, _settings.UploadErrorRoutingKey, ct);
            await DeclareAndBindAsync(_settings.ReportResultQueue, _settings.ReportResultRoutingKey, ct);
            await DeclareAndBindAsync(_settings.ReportErrorQueue, _settings.ReportErrorRoutingKey, ct);

            _logger.LogInformation(
                "RabbitMQ initialized. Exchange={Exchange}, Queues=[{AnalyzeQueue}, {ReportQueue}]",
                _settings.ExchangeName, _settings.AnalyzeReviewsQueue, _settings.GenerateReportQueue);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task DeclareAndBindAsync(string queue, string routingKey, CancellationToken ct)
    {
        await _channel!.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await _channel.QueueBindAsync(
            queue: queue,
            exchange: _settings.ExchangeName,
            routingKey: routingKey,
            cancellationToken: ct);
    }

    public Task PublishAnalyzeReviewsAsync(AnalyzeReviewsMessage message, CancellationToken ct = default)
        => PublishAsync(_settings.AnalyzeReviewsRoutingKey, message, ct);

    public Task PublishGenerateReportAsync(GenerateReportMessage message, CancellationToken ct = default)
        => PublishAsync(_settings.GenerateReportRoutingKey, message, ct);

    private async Task PublishAsync<T>(string routingKey, T payload, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel!.BasicPublishAsync(
            exchange: _settings.ExchangeName,
            routingKey: routingKey,
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
