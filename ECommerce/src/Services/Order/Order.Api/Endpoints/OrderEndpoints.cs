using Messaging;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Api.Domain;
using Order.Api.Infrastructure;

namespace Order.Api.Endpoints;

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);

public sealed record CreateOrderRequest(Guid CustomerId, List<CreateOrderItemRequest>? Items);

public sealed record OrderItemResponse(Guid ProductId, int Quantity);

public sealed record OrderResponse(
    Guid OrderId, Guid CustomerId, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    IReadOnlyList<OrderItemResponse> Items);

public static class CreateOrderValidator
{
    public static Dictionary<string, string[]> Validate(CreateOrderRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (request.CustomerId == Guid.Empty)
        {
            errors["customerId"] = ["CustomerId is required."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors["items"] = ["At least one item is required."];
            return errors;
        }

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (item is null || item.ProductId == Guid.Empty)
            {
                errors[$"items[{i}].productId"] = ["ProductId is required."];
            }

            if (item is null || item.Quantity <= 0)
            {
                errors[$"items[{i}].quantity"] = ["Quantity must be greater than zero."];
            }
        }

        return errors;
    }
}

public static class OrderEndpoints
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapPost("/", async (CreateOrderRequest? request, HttpContext http, OrderDbContext db, CancellationToken ct) =>
        {
            var errors = CreateOrderValidator.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var correlationId = Guid.TryParse(http.Request.Headers[CorrelationHeader], out var c) ? c : Guid.NewGuid();
            var order = Domain.Order.Create(request!.CustomerId, request.Items!.Select(i => (i.ProductId, i.Quantity)));

            var payload = new OrderCreatedPayload(order.Id, order.CustomerId,
                order.Items.Select(i => new OrderItemPayload(i.ProductId, i.Quantity)).ToList());
            var envelope = EventEnvelope<OrderCreatedPayload>.Create(EventTypes.OrderCreated, payload, correlationId);

            // Sipariş ve outbox kaydı aynı transaction'da (tek SaveChanges) yazılır.
            db.Orders.Add(order);
            db.OutboxMessages.Add(OutboxModelBuilderExtensions.CreateOutboxMessage(envelope, Topology.RoutingKeys.OrderCreated));
            await db.SaveChangesAsync(ct);

            http.Response.Headers[CorrelationHeader] = correlationId.ToString();
            return Results.Accepted($"/api/orders/{order.Id}", new { orderId = order.Id, status = order.Status.ToString() });
        });

        group.MapGet("/{orderId:guid}", async (Guid orderId, OrderDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.AsNoTracking().Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            return order is null
                ? Results.NotFound()
                : Results.Ok(new OrderResponse(order.Id, order.CustomerId, order.Status.ToString(),
                    order.CreatedAtUtc, order.UpdatedAtUtc,
                    order.Items.Select(i => new OrderItemResponse(i.ProductId, i.Quantity)).ToList()));
        });
    }
}
