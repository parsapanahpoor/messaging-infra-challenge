
# RabbitMQ Messaging Infrastructure Challenge

A comprehensive .NET 8 implementation demonstrating Work Queue and Fanout patterns using RabbitMQ for distributed logging infrastructure.

## System Architecture

The system implements two distinct patterns:

**Error Logs (Work Queue Pattern):** Fair distribution of messages among Workers. Each message is processed by exactly one Worker.

**Info Logs (Fanout Pattern):** Broadcasting messages to all active Subscribers simultaneously.

## Prerequisites

- .NET 8 SDK or higher
- RabbitMQ Server (local or Docker)
- Docker (optional, for running RabbitMQ)

## Installing RabbitMQ with Docker
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```
Access Management UI: http://localhost:15672

Default credentials: guest / guest

## Project Structure


MessagingInfra/
├── Common/
│   ├── Models/
│   │   ├── ErrorLog.cs
│   │   └── InfoLog.cs
│   └── Config/
│       └── RabbitConfig.cs
├── Producer/
│   ├── Producer.csproj
│   └── Program.cs
├── ErrorWorker/
│   ├── ErrorWorker.csproj
│   └── Program.cs
└── InfoSubscriber/
├── InfoSubscriber.csproj
└── Program.cs

## Setting Environment Variables

**Linux/macOS:**

```bash
export RABBITMQ_HOST=localhost
export RABBITMQ_PORT=5672
export RABBITMQ_USER=guest
export RABBITMQ_PASS=guest
export RABBITMQ_VHOST=/
```
**Windows PowerShell:**

```powershell
$env:RABBITMQ_HOST="localhost"
$env:RABBITMQ_PORT="5672"
$env:RABBITMQ_USER="guest"
$env:RABBITMQ_PASS="guest"
$env:RABBITMQ_VHOST="/"
```
## Running the Producer

```bash
dotnet run --project Producer/Producer.csproj
```
## Running Error Workers

**Terminal 1:**

```bash
dotnet run --project ErrorWorker/ErrorWorker.csproj WorkerA
```
**Terminal 2:**

```bash
dotnet run --project ErrorWorker/ErrorWorker.csproj WorkerB
```
## Running Info Subscribers

**Terminal 1:**

```bash
dotnet run --project InfoSubscriber/InfoSubscriber.csproj elk
```
**Terminal 2:**

```bash
dotnet run --project InfoSubscriber/InfoSubscriber.csproj grafana
```
## Testing Work Queue Pattern

**Goal:** Verify fair distribution of messages among Workers without duplication.

**Steps:**

1. Run two Workers (WorkerA and WorkerB)
2. Run the Producer
3. Verify that each Error message is processed by only one Worker

**Expected Result:** WorkerA and WorkerB receive different messages (e.g., WorkerA: E-1000, E-1002 and WorkerB: E-1001, E-1003)

## Testing Fanout Pattern

**Goal:** Verify simultaneous message delivery to all Subscribers.

**Steps:**

1. Run two Subscribers (elk and grafana)
2. Run the Producer
3. Verify that both Subscribers receive all Info messages

**Expected Result:** Both Subscribers receive identical messages with the same IDs (e.g., both receive I-5000)

## Key Implementation Details

**Work Queue:**

- Manual ACK to prevent message loss
- Set prefetchCount to 1 for fair distribution
- Durable Queue

```csharp
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
channel.BasicConsume(queue: "logs.error.q", autoAck: false, consumer: consumer);
```
**Fanout Pattern:**

- Fanout Exchange for broadcasting
- Each Subscriber has its own dedicated queue
- Queues bound to Exchange without Routing Key

```csharp
channel.ExchangeDeclare("logs.info.exchange", ExchangeType.Fanout, durable: true);
string queueName = $"logs.info.q.{subscriberId}";
channel.QueueBind(queue: queueName, exchange: "logs.info.exchange", routingKey: "");
```
## Common Troubleshooting

**Issue: Connection refused**

Verify that RabbitMQ is running:

```bash
docker ps | grep rabbitmq
```
**Issue: Messages not being received**

Check the RabbitMQ Management UI and verify that Queues and Exchanges are created correctly.

**Issue: Uneven distribution in Work Queue**

Ensure that prefetchCount is set to 1.

## Resources

- RabbitMQ Tutorials: https://www.rabbitmq.com/tutorials/
- RabbitMQ .NET Client: https://www.rabbitmq.com/dotnet.html

