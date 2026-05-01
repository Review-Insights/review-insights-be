using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReviewInsights.Api.Common;
using ReviewInsights.Api.Configuration;
using ReviewInsights.Api.Features.Worker;

namespace ReviewInsights.Api.Infrastructure;

public class WorkerResultsConsumer : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)
        }
    };

    private readonly RabbitMqSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerResultsConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public WorkerResultsConsumer(
        RabbitMqSettings settings,
        IServiceScopeFactory scopeFactory,
        ILogger<WorkerResultsConsumer> logger)
    {
        _settings = settings;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            Port = _settings.Port,
            UserName = _settings.UserName,
            Password = _settings.Password
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: cancellationToken);

        await DeclareAndBindAsync(_settings.UploadResultsQueue, _settings.UploadResultsRoutingKey, cancellationToken);
        await DeclareAndBindAsync(_settings.UploadErrorQueue, _settings.UploadErrorRoutingKey, cancellationToken);
        await DeclareAndBindAsync(_settings.ReportResultQueue, _settings.ReportResultRoutingKey, cancellationToken);
        await DeclareAndBindAsync(_settings.ReportErrorQueue, _settings.ReportErrorRoutingKey, cancellationToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: cancellationToken);

        await StartConsumerAsync(_settings.UploadResultsQueue, HandleUploadResultsAsync, cancellationToken);
        await StartConsumerAsync(_settings.UploadErrorQueue, HandleUploadErrorAsync, cancellationToken);
        await StartConsumerAsync(_settings.ReportResultQueue, HandleReportResultAsync, cancellationToken);
        await StartConsumerAsync(_settings.ReportErrorQueue, HandleReportErrorAsync, cancellationToken);

        _logger.LogInformation(
            "WorkerResultsConsumer started. Queues=[{UploadResults}, {UploadErrors}, {ReportResult}, {ReportErrors}]",
            _settings.UploadResultsQueue, _settings.UploadErrorQueue,
            _settings.ReportResultQueue, _settings.ReportErrorQueue);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);
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

    private Task StartConsumerAsync(
        string queue,
        Func<byte[], CancellationToken, Task> handler,
        CancellationToken ct)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            try
            {
                await handler(body, ct);
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message from {Queue}: {Body}", queue, Encoding.UTF8.GetString(body));
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found for message from {Queue}", queue);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing message from {Queue}, dropping to avoid requeue loop", queue);
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        };

        return _channel!.BasicConsumeAsync(
            queue: queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);
    }

    private async Task HandleUploadResultsAsync(byte[] body, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<WorkerUploadResultsMessage>(body, JsonOptions)
            ?? throw new JsonException("Null WorkerUploadResultsMessage");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkerService>();
        await service.PatchAnalyzeResultsAsync(
            message.UploadId,
            new WorkerAnalyzeResultsRequest { Results = message.Results ?? [] },
            ct);
    }

    private async Task HandleUploadErrorAsync(byte[] body, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<WorkerUploadErrorMessage>(body, JsonOptions)
            ?? throw new JsonException("Null WorkerUploadErrorMessage");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkerService>();
        await service.RegisterUploadErrorAsync(
            message.UploadId,
            new WorkerErrorRequest { ErrorMessage = message.ErrorMessage ?? string.Empty },
            ct);
    }

    private async Task HandleReportResultAsync(byte[] body, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<WorkerReportResultMessage>(body, JsonOptions)
            ?? throw new JsonException("Null WorkerReportResultMessage");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkerService>();
        await service.PatchReportResultAsync(
            message.ReportId,
            new WorkerReportResultRequest
            {
                Summary = message.Summary ?? new Domain.ValueObjects.ReportSummary(),
                Insights = message.Insights ?? [],
                Suggestions = message.Suggestions ?? []
            },
            ct);
    }

    private async Task HandleReportErrorAsync(byte[] body, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<WorkerReportErrorMessage>(body, JsonOptions)
            ?? throw new JsonException("Null WorkerReportErrorMessage");

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WorkerService>();
        await service.RegisterReportErrorAsync(
            message.ReportId,
            new WorkerErrorRequest { ErrorMessage = message.ErrorMessage ?? string.Empty },
            ct);
    }

    private class WorkerUploadResultsMessage
    {
        public Guid UploadId { get; set; }
        public List<WorkerReviewResultDto>? Results { get; set; }
    }

    private class WorkerUploadErrorMessage
    {
        public Guid UploadId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    private class WorkerReportResultMessage
    {
        public Guid ReportId { get; set; }
        public Domain.ValueObjects.ReportSummary? Summary { get; set; }
        public List<Domain.ValueObjects.ReportInsight>? Insights { get; set; }
        public List<Domain.ValueObjects.ReportSuggestion>? Suggestions { get; set; }
    }

    private class WorkerReportErrorMessage
    {
        public Guid ReportId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
