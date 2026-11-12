namespace Messaging.Contracts;

public sealed record OrderItemPayload(Guid ProductId, int Quantity);
