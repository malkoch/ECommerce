namespace Messaging.Contracts;

public sealed record OrderRejectedPayload(Guid OrderId, Guid CustomerId, string ReasonCode);
