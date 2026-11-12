namespace Messaging.Contracts;

public sealed record OrderConfirmedPayload(Guid OrderId, Guid CustomerId);
