namespace Order.Api.Endpoints.Responses;

public sealed record CreateOrderResponse(Guid OrderId, string Status);
