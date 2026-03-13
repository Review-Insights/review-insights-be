namespace debil_be.Configuration;

public class RabbitMqSettings
{
    public string HostName { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public string RoutingKey { get; set; } = string.Empty;
    /// <summary>Queue from which BE consumes analysis results (sent by Kedro worker).</summary>
    public string ResultsQueueName { get; set; } = "debil.blueprint.results";
}
