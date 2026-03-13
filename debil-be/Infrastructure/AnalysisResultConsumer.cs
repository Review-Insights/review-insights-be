using System.Text;
using System.Text.Json;
using debil_be.Configuration;
using debil_be.DTOs;
using debil_be.Messaging;
using debil_be.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace debil_be.Infrastructure;

/// <summary>
/// Consumes analysis result messages from RabbitMQ (sent by Kedro worker).
/// Applies the same logic as HTTP endpoints: update status, add rows, save metrics.
/// </summary>
public class AnalysisResultConsumer : IHostedService, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalysisResultConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AnalysisResultConsumer(RabbitMqSettings settings, IServiceProvider serviceProvider, ILogger<AnalysisResultConsumer> logger)
    {
        _settings = settings;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private async Task EnsureStartedAsync(CancellationToken ct)
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

            _logger.LogInformation("AnalysisResultConsumer connected to RabbitMQ {Host}:{Port}, results queue={Queue}",
                _settings.HostName, _settings.Port, _settings.ResultsQueueName);

            await _channel.QueueDeclareAsync(
                queue: _settings.ResultsQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;

            await _channel.BasicConsumeAsync(
                queue: _settings.ResultsQueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: ct);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var channel = _channel!;
        string? body = null;
        try
        {
            body = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("Received analysis result message: {BodySnippet}...", 
                body.Length > 300 ? body[..300] : body);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var message = JsonSerializer.Deserialize<AnalysisResultMessage>(body, options);
            if (message is null)
            {
                _logger.LogWarning("Failed to deserialize AnalysisResultMessage from body.");
                await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                return;
            }

            if (!Guid.TryParse(message.AnalysisId, out var analysisId))
            {
                _logger.LogWarning("Invalid AnalysisId in message: {AnalysisId}", message.AnalysisId);
                await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var analysisService = scope.ServiceProvider.GetRequiredService<IAnalysisService>();

            _logger.LogInformation("Updating analysis {AnalysisId} status to {Status} with {RowCount} rows",
                analysisId, message.Status, message.Rows.Count);

            // 1. Update status (Completed or Failed)
            await analysisService.UpdateStatusAsync(analysisId, new UpdateAnalysisStatusRequest
            {
                Status = message.Status,
                RecordCount = message.Rows.Count,
                InputColumns = message.InputColumns,
                OutputColumns = message.OutputColumns,
            }, CancellationToken.None);

            // 2. Add rows if any
            if (message.Rows.Count > 0)
            {
                var rowsRequest = new AddAnalysisRowsRequest
                {
                    Rows = message.Rows.Select(r => new AnalysisRowDto
                    {
                        Input = r.Input,
                        Output = r.Output
                    }).ToList()
                };
                await analysisService.AddRowsAsync(analysisId, rowsRequest, CancellationToken.None);
            }

            // 3. Save task metrics if present
            if (message.Metrics is { Tasks: { Count: > 0 } } metrics)
            {
                _logger.LogInformation("Saving {TaskCount} task metrics for analysis {AnalysisId}",
                    metrics.Tasks.Count, analysisId);
                var metricsRequest = new SaveTaskMetricsRequest
                {
                    OverallAvgConfidence = metrics.OverallAvgConfidence,
                    OverallAccuracy = metrics.OverallAccuracy,
                    Tasks = metrics.Tasks.Select(t => new SaveTaskMetricItem
                    {
                        TaskId = t.TaskId,
                        TaskType = t.TaskType,
                        TaskName = t.TaskName,
                        ModelName = t.ModelName,
                        RecordCount = t.RecordCount,
                        AvgConfidence = t.AvgConfidence,
                        MinConfidence = t.MinConfidence,
                        MaxConfidence = t.MaxConfidence,
                        Accuracy = t.Accuracy,
                        Precision = t.Precision,
                        Recall = t.Recall,
                        F1Score = t.F1Score,
                        AucRoc = t.AucRoc,
                        Support = t.Support
                    }).ToList()
                };
                await analysisService.SaveTaskMetricsAsync(analysisId, metricsRequest, CancellationToken.None);
            }

            await channel.BasicAckAsync(ea.DeliveryTag, false);
            _logger.LogInformation("Successfully processed analysis result message for {AnalysisId}", analysisId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing analysis result message: {Body}", body);
            try
            {
                await channel!.BasicNackAsync(ea.DeliveryTag, false, false);
            }
            catch
            {
                // ignore
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return EnsureStartedAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);
    }

    public void Dispose() => _initLock.Dispose();
}
