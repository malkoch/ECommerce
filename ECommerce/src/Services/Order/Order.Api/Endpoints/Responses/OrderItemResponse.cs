namespace Order.Api.Endpoints.Responses;

public sealed record OrderItemResponse(Guid ProductId, int Quantity);
