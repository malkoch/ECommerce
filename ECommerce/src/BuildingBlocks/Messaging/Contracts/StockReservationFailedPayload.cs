namespace Messaging.Contracts;

public sealed record StockReservationFailedPayload(Guid OrderId, string ReasonCode, IReadOnlyList<Guid> FailedProductIds);
