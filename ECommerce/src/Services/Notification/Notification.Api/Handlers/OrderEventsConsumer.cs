using Messaging;
using Microsoft.Extensions.Options;
using Notification.Api.Domain;

namespace Notification.Api.Handlers;

public sealed class OrderEventsConsumer(
    IOptions<RabbitMqOptions> options, IServiceScopeFactory scopes, ILogger<OrderEventsConsumer> logger)
    : RabbitMqConsumer(options, logger)
{
    protected override string QueueName => Topology.Queues.NotificationOrderEvents;

    protected override async Task HandleAsync(ConsumedMessage message, CancellationToken ct)
    {
        var notification = message.EventType switch
        {
            EventTypes.OrderConfirmed => Map(message.ReadEnvelope<OrderConfirmedPayload>(),
                p => (p.OrderId, p.CustomerId, (string?)null)),
            EventTypes.OrderRejected => Map(message.ReadEnvelope<OrderRejectedPayload>(),
                p => (p.OrderId, p.CustomerId, p.ReasonCode)),
            _ => throw new PermanentMessageException($"Unexpected event type '{message.EventType}'.")
        };

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SendNotificationHandler>().HandleAsync(notification, ct);
    }

    private static OrderResultNotification Map<T>(EventEnvelope<T> e, Func<T, (Guid OrderId, Guid CustomerId, string? Reason)> select)
    {
        var (orderId, customerId, reason) = select(e.Payload);
        return new OrderResultNotification(e.MessageId, e.CorrelationId, e.EventType, orderId, customerId, reason);
    }
}
