namespace Order.Api.Endpoints.Requests;

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
