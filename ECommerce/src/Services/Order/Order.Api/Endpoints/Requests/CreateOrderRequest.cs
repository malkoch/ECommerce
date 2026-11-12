namespace Order.Api.Endpoints.Requests;

public sealed record CreateOrderRequest(Guid CustomerId, List<CreateOrderItemRequest>? Items);
