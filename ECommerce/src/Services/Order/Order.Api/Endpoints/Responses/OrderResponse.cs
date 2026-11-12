namespace Order.Api.Endpoints.Responses;

public sealed record OrderResponse(
    Guid OrderId, Guid CustomerId, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    IReadOnlyList<OrderItemResponse> Items);
