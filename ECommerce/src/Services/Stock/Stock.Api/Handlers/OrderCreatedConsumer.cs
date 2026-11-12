using Messaging;
using Microsoft.Extensions.Options;

namespace Stock.Api.Handlers;

public sealed class OrderCreatedConsumer(
    IOptions<RabbitMqOptions> options, IServiceScopeFactory scopes, ILogger<OrderCreatedConsumer> logger)
    : RabbitMqConsumer(options, logger)
{
    protected override string QueueName => Topology.Queues.StockOrderCreated;

    protected override async Task HandleAsync(ConsumedMessage message, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReserveStockHandler>();
        await handler.HandleAsync(message.ReadEnvelope<OrderCreatedPayload>(), ct);
    }
}
