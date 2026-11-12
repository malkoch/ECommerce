namespace Messaging.Contracts;

public sealed record StockReservationSucceededPayload(Guid OrderId, Guid ReservationId);
