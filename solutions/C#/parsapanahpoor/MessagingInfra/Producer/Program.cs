using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using MessagingInfra.Common;
using MessagingInfra.Common.Models;

Console.WriteLine("=== RabbitMQ Producer - Logging Infrastructure ===\n");

var uri = RabbitMQConfig.GetAmqpUri();
Console.WriteLine($"[Producer] Connecting to: {uri.Replace(uri.Split('@')[0].Split("//")[1], "***")}");

var factory = new ConnectionFactory { Uri = new Uri(uri), DispatchConsumersAsync = true };

// Retry mechanism for connection
var maxRetries = 5;
var retryDelay = TimeSpan.FromSeconds(2);

IConnection? connection = null;
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        Console.WriteLine($"[Producer] Connection attempt {attempt}/{maxRetries}...");
        connection = factory.CreateConnection();
        Console.WriteLine("[Producer] ✅ Connected to RabbitMQ");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Producer] ❌ Connection failed: {ex.Message}");
        if (attempt < maxRetries)
        {
            Console.WriteLine($"[Producer] Retrying in {retryDelay.TotalSeconds}s...");
            await Task.Delay(retryDelay);
        }
        else
        {
            Console.WriteLine("[Producer] ❌ Max retries reached. Exiting.");
            return;
        }
    }
}

if (connection == null)
{
    Console.WriteLine("[Producer] ❌ Failed to establish connection. Exiting.");
    return;
}

using var channel = connection.CreateModel();

// Enable Publisher Confirms for reliability
channel.ConfirmSelect();

#region Error Exchange & Queue Setup

channel.ExchangeDeclare(
    exchange: RabbitMQConfig.ErrorExchange,
    type: ExchangeType.Direct,
    durable: true,
    autoDelete: false
);

channel.QueueDeclare(
    queue: RabbitMQConfig.ErrorQueue,
    durable: true,
    exclusive: false,
    autoDelete: false
);

channel.QueueBind(
    queue: RabbitMQConfig.ErrorQueue,
    exchange: RabbitMQConfig.ErrorExchange,
    routingKey: RabbitMQConfig.ErrorRoutingKey
);

Console.WriteLine($"[Producer] ✅ Error Exchange: {RabbitMQConfig.ErrorExchange}");
Console.WriteLine($"[Producer] ✅ Error Queue: {RabbitMQConfig.ErrorQueue}");

#endregion

#region Info Exchange Setup

channel.ExchangeDeclare(
    exchange: RabbitMQConfig.InfoExchange,
    type: ExchangeType.Fanout,
    durable: true,
    autoDelete: false
);

Console.WriteLine($"[Producer] ✅ Info Exchange: {RabbitMQConfig.InfoExchange}");

#endregion

Console.WriteLine("\n[Producer] Starting message publishing. Press Ctrl+C to exit.\n");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n[Producer] Shutting down gracefully...");
};

int errorCount = 1000;
int infoCount = 5000;

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        #region Publish Error Log

        var errorLog = new ErrorLog(
            Id: $"E-{errorCount++}",
            Service: GetRandomService(),
            Message: GetRandomErrorMessage(),
            Severity: GetRandomSeverity(),
            Timestamp: DateTime.UtcNow
        );

        var errorBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorLog));
        var errorProps = channel.CreateBasicProperties();
        errorProps.Persistent = true;
        errorProps.ContentType = "application/json";

        channel.BasicPublish(
            exchange: RabbitMQConfig.ErrorExchange,
            routingKey: RabbitMQConfig.ErrorRoutingKey,
            basicProperties: errorProps,
            body: errorBody
        );

        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Producer] Sent Error id={errorLog.Id} service={errorLog.Service} msg=\"{errorLog.Message}\" severity={errorLog.Severity}");
        Console.ResetColor();

        await Task.Delay(1000, cts.Token);

        #endregion

        #region Publish Info Log

        var infoLog = new InfoLog(
            Id: $"I-{infoCount++}",
            Service: GetRandomService(),
            Message: GetRandomInfoMessage(),
            LatencyMs: Random.Shared.Next(10, 500),
            Timestamp: DateTime.UtcNow
        );

        var infoBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(infoLog));
        var infoProps = channel.CreateBasicProperties();
        infoProps.Persistent = true;
        infoProps.ContentType = "application/json";

        channel.BasicPublish(
            exchange: RabbitMQConfig.InfoExchange,
            routingKey: "",
            basicProperties: infoProps,
            body: infoBody
        );

        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Producer] Sent Info  id={infoLog.Id} service={infoLog.Service} msg=\"{infoLog.Message}\" latency_ms={infoLog.LatencyMs}");
        Console.ResetColor();

        await Task.Delay(1000, cts.Token);

        #endregion
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("[Producer] Cancelled by user.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Producer] ❌ Error: {ex.Message}");
}
finally
{
    channel.Close();
    connection.Close();
    Console.WriteLine("[Producer] 🛑 Connection closed.");
}

#region Helper Methods

static string GetRandomService()
    => new[] { "auth", "web", "api", "db", "cache" }[Random.Shared.Next(5)];

static string GetRandomErrorMessage()
    => new[] { "DB timeout", "Connection failed", "Null reference", "Out of memory", "Deadlock detected" }[Random.Shared.Next(5)];

static string GetRandomSeverity()
    => new[] { "HIGH", "CRITICAL", "MEDIUM" }[Random.Shared.Next(3)];

static string GetRandomInfoMessage()
    => new[] { "GET /api/orders 200", "POST /api/users 201", "PUT /api/products 200", "DELETE /api/items 204" }[Random.Shared.Next(4)];

#endregion
