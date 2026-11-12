namespace Messaging.Contracts;

public sealed record OrderCreatedPayload(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderItemPayload> Items);
