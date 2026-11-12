using Messaging;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using OrderEntity = Order.Api.Domain.Entities.Order;
using Order.Api.Infrastructure;

namespace Order.Api.Endpoints;

public static class OrderEndpoints
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapPost("/", async (CreateOrderRequest? request, HttpContext http, IOrderRepository repository, CancellationToken ct) =>
        {
            var errors = CreateOrderValidator.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var correlationId = Guid.TryParse(http.Request.Headers[CorrelationHeader], out var c) ? c : Guid.NewGuid();
            var order = OrderEntity.Create(request!.CustomerId, request.Items!.Select(i => (i.ProductId, i.Quantity)));

            var payload = new OrderCreatedPayload(order.Id, order.CustomerId,
                order.Items.Select(i => new OrderItemPayload(i.ProductId, i.Quantity)).ToList());
            var envelope = EventEnvelope<OrderCreatedPayload>.Create(EventTypes.OrderCreated, payload, correlationId);

            // Sipariş ve outbox kaydı aynı transaction'da (tek SaveChanges) yazılır.
            repository.Add(order);
            repository.AddOutboxMessage(OutboxModelBuilderExtensions.CreateOutboxMessage(envelope, Topology.RoutingKeys.OrderCreated));
            await repository.SaveChangesAsync(ct);

            http.Response.Headers[CorrelationHeader] = correlationId.ToString();
            return Results.Accepted($"/api/orders/{order.Id}", new CreateOrderResponse(order.Id, order.Status.ToString()));
        })
        .WithName("CreateOrder")
        .WithSummary("Creates an order")
        .WithDescription("Persists the order and publishes an OrderCreated event. Optional X-Correlation-Id header (GUID) is propagated.")
        .Accepts<CreateOrderRequest>("application/json")
        .Produces<CreateOrderResponse>(StatusCodes.Status202Accepted)
        .ProducesValidationProblem();

        group.MapGet("/{orderId:guid}", async (Guid orderId, IOrderRepository repository, CancellationToken ct) =>
        {
            var order = await repository.GetWithItemsAsync(orderId, ct);

            return order is null
                ? Results.NotFound()
                : Results.Ok(new OrderResponse(order.Id, order.CustomerId, order.Status.ToString(),
                    order.CreatedAtUtc, order.UpdatedAtUtc,
                    order.Items.Select(i => new OrderItemResponse(i.ProductId, i.Quantity)).ToList()));
        })
        .WithName("GetOrder")
        .WithSummary("Gets an order by id")
        .Produces<OrderResponse>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
