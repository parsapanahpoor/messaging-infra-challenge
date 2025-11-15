using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MessagingInfra.Common;
using MessagingInfra.Common.Models;

var workerId = args.Length > 0 ? args[0] : Guid.NewGuid().ToString("N")[..8];

Console.WriteLine($"=== Error Worker [{workerId}] ===\n");

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
        Console.WriteLine($"[ErrorWorker-{workerId}] Connecting (attempt {attempt}/{maxRetries})...");
        connection = factory.CreateConnection();
        Console.WriteLine($"[ErrorWorker-{workerId}] ✅ Connected");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ErrorWorker-{workerId}] ❌ Failed: {ex.Message}");
        if (attempt < maxRetries)
        {
            Console.WriteLine($"[ErrorWorker-{workerId}] Retrying in {retryDelay.TotalSeconds}s...");
            await Task.Delay(retryDelay);
        }
        else
        {
            Console.WriteLine($"[ErrorWorker-{workerId}] ❌ Max retries reached. Exiting.");
            return;
        }
    }
}

if (connection == null) return;

using var channel = connection.CreateModel();

// Fair dispatch: Prefetch = 1 (one message at a time)
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

// Ensure queue exists
channel.QueueDeclare(
    queue: RabbitMQConfig.ErrorQueue,
    durable: true,
    exclusive: false,
    autoDelete: false
);

Console.WriteLine($"[ErrorWorker-{workerId}] ✅ Listening on: {RabbitMQConfig.ErrorQueue}");
Console.WriteLine($"[ErrorWorker-{workerId}] Prefetch: 1 (Fair Dispatch)\n");

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.Received += async (sender, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);

    try
    {
        var log = JsonSerializer.Deserialize<ErrorLog>(message);
        if (log == null)
        {
            Console.WriteLine($"[ErrorWorker-{workerId}] ❌ Invalid message format");
            channel.BasicNack(ea.DeliveryTag, false, false);
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[ErrorWorker-{workerId}] {log.Id} received ... processing ...");
        Console.ResetColor();

        // Simulate processing
        await Task.Delay(Random.Shared.Next(1000, 3000));

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[ErrorWorker-{workerId}] {log.Id} ✅ ACKED (Service: {log.Service}, Severity: {log.Severity})");
        Console.ResetColor();

        // Manual ACK
        channel.BasicAck(ea.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ErrorWorker-{workerId}] ❌ Processing failed: {ex.Message}");
        Console.ResetColor();

        // NACK and requeue
        channel.BasicNack(ea.DeliveryTag, false, true);
    }
};

channel.BasicConsume(
    queue: RabbitMQConfig.ErrorQueue,
    autoAck: false,
    consumer: consumer
);

Console.WriteLine($"[ErrorWorker-{workerId}] Press Ctrl+C to exit.\n");

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
    Console.WriteLine($"\n[ErrorWorker-{workerId}] Shutting down...");
}
finally
{
    channel.Close();
    connection.Close();
    Console.WriteLine($"[ErrorWorker-{workerId}] 🛑 Connection closed.");
}
