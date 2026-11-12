namespace Messaging.Contracts;

public static class EventTypes
{
    public const string OrderCreated = nameof(OrderCreated);
    public const string StockReservationSucceeded = nameof(StockReservationSucceeded);
    public const string StockReservationFailed = nameof(StockReservationFailed);
    public const string OrderConfirmed = nameof(OrderConfirmed);
    public const string OrderRejected = nameof(OrderRejected);
}
