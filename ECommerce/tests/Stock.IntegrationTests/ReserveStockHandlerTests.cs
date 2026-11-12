using Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Stock.Api.Handlers;

namespace Stock.IntegrationTests;

public class ReserveStockHandlerTests(StockDatabaseFixture fixture) : IClassFixture<StockDatabaseFixture>
{
    private static EventEnvelope<OrderCreatedPayload> Order(params (Guid Product, int Qty)[] items) =>
        EventEnvelope<OrderCreatedPayload>.Create(EventTypes.OrderCreated,
            new OrderCreatedPayload(Guid.NewGuid(), Guid.NewGuid(),
                items.Select(i => new OrderItemPayload(i.Product, i.Qty)).ToList()),
            Guid.NewGuid());

    private async Task<ReservationOutcome> HandleAsync(EventEnvelope<OrderCreatedPayload> envelope)
    {
        await using var db = fixture.CreateContext();
        return await new ReserveStockHandler(db, NullLogger<ReserveStockHandler>.Instance)
            .HandleAsync(envelope, CancellationToken.None);
    }

    private async Task<List<string>> OutboxRoutingKeysAsync(Guid orderId)
    {
        await using var db = fixture.CreateContext();
        var messages = await db.OutboxMessages.AsNoTracking().ToListAsync();
        return messages.Where(m => m.Payload.Contains(orderId.ToString())).Select(m => m.RoutingKey).ToList();
    }

    [Fact]
    public async Task SufficientStock_DecrementsOnceAndPublishesSuccess()
    {
        var product = await fixture.AddProductAsync(10);
        var order = Order((product, 4));

        Assert.Equal(ReservationOutcome.Succeeded, await HandleAsync(order));

        Assert.Equal(6, await fixture.GetQuantityAsync(product));
        Assert.Equal([Topology.RoutingKeys.StockReservationSucceeded], await OutboxRoutingKeysAsync(order.Payload.OrderId));
    }

    [Fact]
    public async Task InsufficientStock_DoesNotGoNegativeAndPublishesFailure()
    {
        var product = await fixture.AddProductAsync(3);
        var order = Order((product, 5));

        Assert.Equal(ReservationOutcome.Failed, await HandleAsync(order));

        Assert.Equal(3, await fixture.GetQuantityAsync(product));
        Assert.Equal([Topology.RoutingKeys.StockReservationFailed], await OutboxRoutingKeysAsync(order.Payload.OrderId));
    }

    [Fact]
    public async Task OneInsufficientItem_RollsBackOtherItems()
    {
        var plenty = await fixture.AddProductAsync(10);
        var scarce = await fixture.AddProductAsync(1);

        Assert.Equal(ReservationOutcome.Failed, await HandleAsync(Order((plenty, 5), (scarce, 2))));

        Assert.Equal(10, await fixture.GetQuantityAsync(plenty));
        Assert.Equal(1, await fixture.GetQuantityAsync(scarce));
    }

    [Fact]
    public async Task UnknownProduct_Fails()
    {
        Assert.Equal(ReservationOutcome.Failed, await HandleAsync(Order((Guid.NewGuid(), 1))));
    }

    [Fact]
    public async Task SameMessageTwice_DecrementsOnce()
    {
        var product = await fixture.AddProductAsync(10);
        var order = Order((product, 3));

        Assert.Equal(ReservationOutcome.Succeeded, await HandleAsync(order));
        Assert.Equal(ReservationOutcome.Duplicate, await HandleAsync(order));

        Assert.Equal(7, await fixture.GetQuantityAsync(product));
        Assert.Single(await OutboxRoutingKeysAsync(order.Payload.OrderId));
    }

    [Fact]
    public async Task SameOrderWithNewMessageId_DecrementsOnce()
    {
        var product = await fixture.AddProductAsync(10);
        var order = Order((product, 3));
        var redelivery = order with { MessageId = Guid.NewGuid() };

        Assert.Equal(ReservationOutcome.Succeeded, await HandleAsync(order));
        Assert.Equal(ReservationOutcome.Duplicate, await HandleAsync(redelivery));

        Assert.Equal(7, await fixture.GetQuantityAsync(product));
    }

    [Fact]
    public async Task ConcurrentOrders_NeverOversell()
    {
        var product = await fixture.AddProductAsync(5);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => HandleAsync(Order((product, 1))))));

        Assert.Equal(5, outcomes.Count(o => o == ReservationOutcome.Succeeded));
        Assert.Equal(7, outcomes.Count(o => o == ReservationOutcome.Failed));
        Assert.Equal(0, await fixture.GetQuantityAsync(product));
    }

    [Fact]
    public async Task HotProduct_ManyConcurrentOrders_ExactlyStockIsSold()
    {
        var product = await fixture.AddProductAsync(100);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 60).Select(_ => Task.Run(() => HandleAsync(Order((product, 2))))));

        Assert.Equal(50, outcomes.Count(o => o == ReservationOutcome.Succeeded));
        Assert.Equal(10, outcomes.Count(o => o == ReservationOutcome.Failed));
        Assert.Equal(0, await fixture.GetQuantityAsync(product));
    }

    [Fact]
    public async Task SameMessageDeliveredInParallel_ReservesOnce()
    {
        var product = await fixture.AddProductAsync(10);
        var order = Order((product, 3));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => HandleAsync(order))));

        Assert.Equal(1, outcomes.Count(o => o == ReservationOutcome.Succeeded));
        Assert.Equal(7, await fixture.GetQuantityAsync(product));
    }
}
