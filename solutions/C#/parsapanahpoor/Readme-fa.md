# RabbitMQ Messaging Infrastructure Challenge

یک پیاده‌سازی جامع با .NET 8 که دو الگوی Work Queue و Fanout را با استفاده از RabbitMQ برای زیرساخت لاگ‌گیری توزیع‌شده نمایش می‌دهد.

## معماری سیستم

سیستم دو الگوی مجزا را پیاده‌سازی می‌کند:

**Error Logs (Work Queue Pattern):** توزیع عادلانه پیام‌ها بین Worker ها. هر پیام توسط دقیقاً یک Worker پردازش می‌شود.

**Info Logs (Fanout Pattern):** ارسال پیام به تمام Subscriber های فعال به صورت همزمان (Broadcast).

## پیش‌نیازها

- .NET 8 SDK یا بالاتر
- RabbitMQ Server (محلی یا Docker)
- Docker (اختیاری، برای اجرای RabbitMQ)

## نصب RabbitMQ با Docker
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```
دسترسی به Management UI: http://localhost:15672

اطلاعات ورود پیش‌فرض: guest / guest


## تنظیم متغیرهای محیطی

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
## اجرای Producer

```bash
dotnet run --project Producer/Producer.csproj
```
## اجرای Error Workers

**Terminal 1:**

```bash
dotnet run --project ErrorWorker/ErrorWorker.csproj WorkerA
```
**Terminal 2:**

```bash
dotnet run --project ErrorWorker/ErrorWorker.csproj WorkerB
```
## اجرای Info Subscribers

**Terminal 1:**

```bash
dotnet run --project InfoSubscriber/InfoSubscriber.csproj elk
```
**Terminal 2:**

```bash
dotnet run --project InfoSubscriber/InfoSubscriber.csproj grafana
```
## تست Work Queue Pattern

**هدف:** تأیید توزیع عادلانه پیام‌ها بین Worker ها بدون تکرار.

**مراحل:**

1. دو Worker (WorkerA و WorkerB) را اجرا کنید
2. Producer را اجرا کنید
3. تأیید کنید که هر پیام Error فقط توسط یک Worker پردازش می‌شود

**نتیجه مورد انتظار:** WorkerA و WorkerB پیام‌های مختلف دریافت می‌کنند (مثلاً WorkerA: E-1000, E-1002 و WorkerB: E-1001, E-1003)

## تست Fanout Pattern

**هدف:** تأیید دریافت همزمان پیام‌ها توسط تمام Subscriber ها.

**مراحل:**

1. دو Subscriber (elk و grafana) را اجرا کنید
2. Producer را اجرا کنید  
3. تأیید کنید که هر دو Subscriber همه پیام‌های Info را دریافت می‌کنند

**نتیجه مورد انتظار:** هر دو Subscriber پیام‌های یکسان با ID های مشابه دریافت می‌کنند (مثلاً هر دو I-5000 را دریافت می‌کنند)

## نکات کلیدی پیاده‌سازی

**Work Queue:**

- استفاده از Manual ACK برای جلوگیری از از دست رفتن پیام
- تنظیم prefetchCount به 1 برای توزیع عادلانه
- Queue دارای ویژگی Durable

```csharp
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
channel.BasicConsume(queue: "logs.error.q", autoAck: false, consumer: consumer);
```
**Fanout Pattern:**

- استفاده از Fanout Exchange برای broadcast
- هر Subscriber صف اختصاصی خود را دارد
- اتصال صف‌ها به Exchange بدون Routing Key

```csharp
channel.ExchangeDeclare("logs.info.exchange", ExchangeType.Fanout, durable: true);
string queueName = $"logs.info.q.{subscriberId}";
channel.QueueBind(queue: queueName, exchange: "logs.info.exchange", routingKey: "");
```
## عیب‌یابی رایج

**مشکل: Connection refused**

بررسی کنید که RabbitMQ در حال اجراست:

```bash
docker ps | grep rabbitmq
```
**مشکل: پیام‌ها دریافت نمی‌شوند**

به RabbitMQ Management UI مراجعه کنید و تأیید کنید که Queue ها و Exchange ها به درستی ایجاد شده‌اند.

**مشکل: توزیع نامتعادل در Work Queue**

مطمئن شوید که prefetchCount روی 1 تنظیم شده است.

## منابع

- RabbitMQ Tutorials: https://www.rabbitmq.com/tutorials/
- RabbitMQ .NET Client: https://www.rabbitmq.com/dotnet.html

