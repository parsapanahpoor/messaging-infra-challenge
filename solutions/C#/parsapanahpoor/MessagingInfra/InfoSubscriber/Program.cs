using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MessagingInfra.Common;
using MessagingInfra.Common.Models;

var serviceName = args.Length > 0 ? args[0] : "unknown";

Console.WriteLine($"=== Info Subscriber [{serviceName}] ===\n");

var uri = RabbitMQConfig.GetAmqpUri();
var factory = new ConnectionFactory { Uri = new Uri(uri), DispatchConsumersAsync = true };

// Retry mechanism
var maxRetries = 5;
var retryDelay = TimeSpan.FromSeconds(2);

IConnection? connection = null;
for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        Console.WriteLine($"[InfoSub-{serviceName}] Connecting (attempt {attempt}/{maxRetries})...");
        connection = factory.CreateConnection();
        Console.WriteLine($"[InfoSub-{serviceName}] ✅ Connected");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[InfoSub-{serviceName}] ❌ Failed: {ex.Message}");
        if (attempt < maxRetries)
        {
            Console.WriteLine($"[InfoSub-{serviceName}] Retrying in {retryDelay.TotalSeconds}s...");
            await Task.Delay(retryDelay);
        }
        else
        {
            Console.WriteLine($"[InfoSub-{serviceName}] ❌ Max retries reached. Exiting.");
            return;
        }
    }
}

if (connection == null) return;

using var channel = connection.CreateModel();

// Ensure Fanout Exchange exists
channel.ExchangeDeclare(
    exchange: RabbitMQConfig.InfoExchange,
    type: ExchangeType.Fanout,
    durable: true,
    autoDelete: false
);

// Create dedicated queue for this subscriber
var queueName = $"{RabbitMQConfig.InfoQueuePrefix}{serviceName}";
channel.QueueDeclare(
    queue: queueName,
    durable: true,
    exclusive: false,
    autoDelete: false
);

// Bind queue to fanout exchange
channel.QueueBind(
    queue: queueName,
    exchange: RabbitMQConfig.InfoExchange,
    routingKey: ""
);

Console.WriteLine($"[InfoSub-{serviceName}] ✅ Listening on: {queueName}");
Console.WriteLine($"[InfoSub-{serviceName}] Bound to: {RabbitMQConfig.InfoExchange}\n");

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.Received += async (sender, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);

    try
    {
        var log = JsonSerializer.Deserialize<InfoLog>(message);
        if (log == null)
        {
            Console.WriteLine($"[InfoSub-{serviceName}] ❌ Invalid message format");
            channel.BasicNack(ea.DeliveryTag, false, false);
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[InfoSub-{serviceName}] {log.Id} -> dashboard updated (Service: {log.Service}, Latency: {log.LatencyMs}ms)");
        Console.ResetColor();

        // Simulate processing
        await Task.Delay(100);

        // Manual ACK
        channel.BasicAck(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[InfoSub-{serviceName}] ❌ Processing failed: {ex.Message}");
        Console.ResetColor();

        channel.BasicNack(ea.DeliveryTag, false, true);
    }
};

channel.BasicConsume(
    queue: queueName,
    autoAck: false,
    consumer: consumer
);

Console.WriteLine($"[InfoSub-{serviceName}] Press Ctrl+C to exit.\n");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine($"\n[InfoSub-{serviceName}] Shutting down...");
}
finally
{
    channel.Close();
    connection.Close();
    Console.WriteLine($"[InfoSub-{serviceName}] 🛑 Connection closed.");
}
