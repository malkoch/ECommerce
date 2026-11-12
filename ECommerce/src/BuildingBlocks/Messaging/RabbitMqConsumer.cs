using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Messaging;

/// <summary>
/// Kuyruğu ve binding'leri idempotent olarak tanımlar, mesajı işleyip handler başarılı olduktan sonra ACK verir.
/// Handler hata fırlatırsa mesaj requeue edilmeden DLQ'ya (`{queue}.dlq`) gönderilir.
/// </summary>
public abstract class RabbitMqConsumer(IOptions<RabbitMqOptions> options, ILogger logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected abstract string QueueName { get; }

    protected abstract Task HandleAsync(ConsumedMessage message, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Broker'a bağlanılamazsa uygulama başlangıcını bloklamadan yeniden denenir.
        await Task.Yield();

        IConnection? connection = null;
        while (!stoppingToken.IsCancellationRequested && connection is null)
        {
            try
            {
                connection = await ConnectAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Consumer {Queue} could not connect to RabbitMQ; retrying in {Delay}s",
                    QueueName, ReconnectDelay.TotalSeconds);
                await Task.Delay(ReconnectDelay, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }

        if (connection is null)
        {
            return;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    private async Task<IConnection> ConnectAsync(CancellationToken ct)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.Host, Port = o.Port, UserName = o.User, Password = o.Password,
            AutomaticRecoveryEnabled = true, TopologyRecoveryEnabled = true
        };
        var connection = await factory.CreateConnectionAsync(ct);
        try
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            await Topology.DeclareAsync(channel, ct);
            await channel.BasicQosAsync(0, 10, false, ct);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, ea) => OnReceivedAsync(channel, ea, ct);
            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, ct);
            logger.LogInformation("Consumer started on queue {Queue}", QueueName);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetString(ea.Body.Span);
        var (messageId, correlationId, eventType) = new ConsumedMessage(body).PeekMetadata();
        var o = options.Value;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ServiceName"] = ServiceName,
            ["CorrelationId"] = correlationId,
            ["MessageId"] = messageId,
            ["EventType"] = eventType
        });

        try
        {
            await MessageRetry.ExecuteAsync(
                attempt => HandleAsync(new ConsumedMessage(body, attempt), ct),
                o.ConsumerMaxAttempts, TimeSpan.FromMilliseconds(o.ConsumerRetryBaseDelayMs),
                (attempt, ex) => logger.LogWarning(ex, "Message handling failed on {Queue}; retrying. Attempt={Attempt}",
                    QueueName, attempt),
                ct);
            await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Message failed on {Queue}; sending to DLQ", QueueName);
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, CancellationToken.None);
        }
    }

    private static readonly string ServiceName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";
}
