using RabbitMQ.Client;

namespace Messaging;

public static class Topology
{
    public const string Exchange = "ecommerce.events";
    public const string DeadLetterExchange = "ecommerce.events.dlx";

    public static class RoutingKeys
    {
        public const string OrderCreated = "order.created";
        public const string StockReservationSucceeded = "stock.reservation.succeeded";
        public const string StockReservationFailed = "stock.reservation.failed";
        public const string OrderConfirmed = "order.confirmed";
        public const string OrderRejected = "order.rejected";
    }

    public static class Queues
    {
        public const string StockOrderCreated = "stock.order-created";
        public const string OrderStockReservationResult = "order.stock-reservation-result";
        public const string NotificationOrderEvents = "notification.order-events";
    }

    public static readonly IReadOnlyDictionary<string, string[]> QueueBindings = new Dictionary<string, string[]>
    {
        [Queues.StockOrderCreated] = [RoutingKeys.OrderCreated],
        [Queues.OrderStockReservationResult] = [RoutingKeys.StockReservationSucceeded, RoutingKeys.StockReservationFailed],
        [Queues.NotificationOrderEvents] = [RoutingKeys.OrderConfirmed, RoutingKeys.OrderRejected]
    };

    /// <summary>Exchange, kuyruk, DLQ ve binding'leri idempotent tanımlar; kuyruklar hangi servis önce açılırsa açılsın mevcut olur.</summary>
    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);

        foreach (var (queue, keys) in QueueBindings)
        {
            var dlq = $"{queue}.dlq";
            await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await channel.QueueBindAsync(dlq, DeadLetterExchange, queue, cancellationToken: ct);

            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = queue
                }, cancellationToken: ct);

            foreach (var key in keys)
            {
                await channel.QueueBindAsync(queue, Exchange, key, cancellationToken: ct);
            }
        }
    }
}
