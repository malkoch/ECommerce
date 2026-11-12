using Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Order.Api.Domain;
using Order.Api.Handlers;
using Order.Api.Infrastructure;

namespace Order.IntegrationTests;

public class StockResultHandlerTests(OrderDatabaseFixture fixture) : IClassFixture<OrderDatabaseFixture>
{
    private static EventEnvelope<StockReservationSucceededPayload> Succeeded(Guid orderId) =>
        EventEnvelope<StockReservationSucceededPayload>.Create(EventTypes.StockReservationSucceeded,
            new StockReservationSucceededPayload(orderId, Guid.NewGuid()), Guid.NewGuid());

    private static EventEnvelope<StockReservationFailedPayload> Failed(Guid orderId) =>
        EventEnvelope<StockReservationFailedPayload>.Create(EventTypes.StockReservationFailed,
            new StockReservationFailedPayload(orderId, "INSUFFICIENT_STOCK", [Guid.NewGuid()]), Guid.NewGuid());

    private async Task<ResultOutcome> HandleAsync(EventEnvelope<StockReservationSucceededPayload> e)
    {
        await using var db = fixture.CreateContext();
        return await new StockResultHandler(new OrderRepository(db), NullLogger<StockResultHandler>.Instance).HandleSucceededAsync(e, default);
    }

    private async Task<ResultOutcome> HandleAsync(EventEnvelope<StockReservationFailedPayload> e)
    {
        await using var db = fixture.CreateContext();
        return await new StockResultHandler(new OrderRepository(db), NullLogger<StockResultHandler>.Instance).HandleFailedAsync(e, default);
    }

    [Fact]
    public async Task Succeeded_ConfirmsOrderAndPublishesOrderConfirmed()
    {
        var order = await fixture.AddOrderAsync();

        Assert.Equal(ResultOutcome.Applied, await HandleAsync(Succeeded(order.Id)));

        Assert.Equal(OrderStatus.Confirmed, await fixture.GetStatusAsync(order.Id));
        Assert.Equal([Topology.RoutingKeys.OrderConfirmed], await fixture.OutboxRoutingKeysAsync(order.Id));
    }

    [Fact]
    public async Task Failed_RejectsOrderAndPublishesOrderRejected()
    {
        var order = await fixture.AddOrderAsync();

        Assert.Equal(ResultOutcome.Applied, await HandleAsync(Failed(order.Id)));

        Assert.Equal(OrderStatus.Rejected, await fixture.GetStatusAsync(order.Id));
        Assert.Equal([Topology.RoutingKeys.OrderRejected], await fixture.OutboxRoutingKeysAsync(order.Id));
    }

    [Fact]
    public async Task SameMessageTwice_PublishesOnce()
    {
        var order = await fixture.AddOrderAsync();
        var message = Succeeded(order.Id);

        await HandleAsync(message);

        Assert.Equal(ResultOutcome.Duplicate, await HandleAsync(message));
        Assert.Single(await fixture.OutboxRoutingKeysAsync(order.Id));
    }

    [Fact]
    public async Task SameResultWithNewMessageId_IsIgnored()
    {
        var order = await fixture.AddOrderAsync();

        await HandleAsync(Succeeded(order.Id));

        Assert.Equal(ResultOutcome.Ignored, await HandleAsync(Succeeded(order.Id)));
        Assert.Single(await fixture.OutboxRoutingKeysAsync(order.Id));
    }

    [Fact]
    public async Task ConflictingLateResult_DoesNotChangeFinalState()
    {
        var order = await fixture.AddOrderAsync();

        await HandleAsync(Succeeded(order.Id));

        Assert.Equal(ResultOutcome.Ignored, await HandleAsync(Failed(order.Id)));
        Assert.Equal(OrderStatus.Confirmed, await fixture.GetStatusAsync(order.Id));
        Assert.Single(await fixture.OutboxRoutingKeysAsync(order.Id));
    }

    [Fact]
    public async Task UnknownOrder_ThrowsAndDoesNotRecordInbox()
    {
        var message = Succeeded(Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => HandleAsync(message));

        // Inbox kaydı rollback edildiği için aynı mesaj tekrar denenebilir (hâlâ hata verir, "Duplicate" dönmez).
        await Assert.ThrowsAsync<InvalidOperationException>(() => HandleAsync(message));
    }

    [Fact]
    public async Task ConcurrentConflictingResults_ProduceSingleFinalStateAndEvent()
    {
        var order = await fixture.AddOrderAsync();

        await Task.WhenAll(
            Task.Run(() => HandleAsync(Succeeded(order.Id))),
            Task.Run(() => HandleAsync(Failed(order.Id))));

        var status = await fixture.GetStatusAsync(order.Id);
        var keys = await fixture.OutboxRoutingKeysAsync(order.Id);
        Assert.NotEqual(OrderStatus.PendingStock, status);
        Assert.Single(keys);
        Assert.Equal(status == OrderStatus.Confirmed ? Topology.RoutingKeys.OrderConfirmed : Topology.RoutingKeys.OrderRejected, keys[0]);
    }
}
