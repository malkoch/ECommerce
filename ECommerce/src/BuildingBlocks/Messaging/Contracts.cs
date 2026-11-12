namespace Messaging;

public sealed record OrderItemPayload(Guid ProductId, int Quantity);

public sealed record OrderCreatedPayload(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderItemPayload> Items);

public sealed record StockReservationSucceededPayload(Guid OrderId, Guid ReservationId);

public sealed record StockReservationFailedPayload(Guid OrderId, string ReasonCode, IReadOnlyList<Guid> FailedProductIds);

public sealed record OrderConfirmedPayload(Guid OrderId, Guid CustomerId);

public sealed record OrderRejectedPayload(Guid OrderId, Guid CustomerId, string ReasonCode);

public static class EventTypes
{
    public const string OrderCreated = nameof(OrderCreated);
    public const string StockReservationSucceeded = nameof(StockReservationSucceeded);
    public const string StockReservationFailed = nameof(StockReservationFailed);
    public const string OrderConfirmed = nameof(OrderConfirmed);
    public const string OrderRejected = nameof(OrderRejected);
}
