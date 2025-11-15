namespace MessagingInfra.Common;

/// <summary>
/// Centralized RabbitMQ configuration for exchanges, queues, and connection settings
/// </summary>
public static class RabbitMQConfig
{
    /// <summary>
    /// Reads RabbitMQ connection string from environment variables
    /// Priority: AMQP_URI > individual components (RABBIT_HOST, RABBIT_USER, etc.)
    /// </summary>
    public static string GetAmqpUri()
    {
        var uri = Environment.GetEnvironmentVariable("AMQP_URI");
        if (!string.IsNullOrEmpty(uri))
            return uri;

        var host = Environment.GetEnvironmentVariable("RABBIT_HOST") ?? "localhost";
        var user = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RABBIT_PASS") ?? "guest";
        var port = Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672";

        return $"amqp://{user}:{pass}@{host}:{port}/";
    }

    // Error Queue Configuration (Work Queue Pattern)
    public const string ErrorExchange = "logs.error.exchange";
    public const string ErrorQueue = "logs.error.q";
    public const string ErrorRoutingKey = "error";

    // Info Queue Configuration (Fanout Pattern)
    public const string InfoExchange = "logs.info.exchange";
    public const string InfoQueuePrefix = "logs.info.q.";
}
