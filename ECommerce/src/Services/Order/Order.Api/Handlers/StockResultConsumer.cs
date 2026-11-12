using Messaging;
using Microsoft.Extensions.Options;

namespace Order.Api.Handlers;

public sealed class StockResultConsumer(
    IOptions<RabbitMqOptions> options, IServiceScopeFactory scopes, ILogger<StockResultConsumer> logger)
    : RabbitMqConsumer(options, logger)
{
    protected override string QueueName => Topology.Queues.OrderStockReservationResult;

    protected override async Task HandleAsync(ConsumedMessage message, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<StockResultHandler>();

        switch (message.EventType)
        {
            case EventTypes.StockReservationSucceeded:
                await handler.HandleSucceededAsync(message.ReadEnvelope<StockReservationSucceededPayload>(), ct);
                break;
            case EventTypes.StockReservationFailed:
                await handler.HandleFailedAsync(message.ReadEnvelope<StockReservationFailedPayload>(), ct);
                break;
            default:
                throw new PermanentMessageException($"Unexpected event type '{message.EventType}'.");
        }
    }
}
