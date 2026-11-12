using OrderEntity = Order.Api.Domain.Entities.Order;

namespace Order.UnitTests;

public class OrderDomainTests
{
    private static OrderEntity NewOrder() =>
        OrderEntity.Create(Guid.NewGuid(), [(Guid.NewGuid(), 2)]);

    [Fact]
    public void Create_StartsAsPendingStock()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.PendingStock, order.Status);
        Assert.Single(order.Items);
    }

    [Fact]
    public void Create_MergesDuplicateProducts()
    {
        var product = Guid.NewGuid();

        var order = OrderEntity.Create(Guid.NewGuid(), [(product, 1), (product, 2)]);

        Assert.Equal(3, Assert.Single(order.Items).Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositiveQuantity(int quantity) =>
        Assert.Throws<ArgumentException>(() =>
            OrderEntity.Create(Guid.NewGuid(), [(Guid.NewGuid(), quantity)]));

    [Fact]
    public void Create_RejectsEmptyItems() =>
        Assert.Throws<ArgumentException>(() => OrderEntity.Create(Guid.NewGuid(), []));

    [Fact]
    public void Confirm_IsIdempotentAndFinal()
    {
        var order = NewOrder();

        Assert.True(order.Confirm());
        Assert.False(order.Confirm());
        Assert.False(order.Reject());
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Reject_DoesNotOverrideConfirmed()
    {
        var order = NewOrder();
        order.Reject();

        Assert.False(order.Confirm());
        Assert.Equal(OrderStatus.Rejected, order.Status);
    }
}

public class CreateOrderValidatorTests
{
    [Fact]
    public void Valid_HasNoErrors() =>
        Assert.Empty(CreateOrderValidator.Validate(
            new CreateOrderRequest(Guid.NewGuid(), [new CreateOrderItemRequest(Guid.NewGuid(), 1)])));

    [Fact]
    public void NullBody_IsInvalid() => Assert.NotEmpty(CreateOrderValidator.Validate(null));

    [Fact]
    public void MissingItems_IsInvalid() =>
        Assert.Contains("items", CreateOrderValidator.Validate(new CreateOrderRequest(Guid.NewGuid(), null)).Keys);

    [Fact]
    public void BadItem_ReportsProductAndQuantity()
    {
        var errors = CreateOrderValidator.Validate(
            new CreateOrderRequest(Guid.Empty, [new CreateOrderItemRequest(Guid.Empty, 0)]));

        Assert.Contains("customerId", errors.Keys);
        Assert.Contains("items[0].productId", errors.Keys);
        Assert.Contains("items[0].quantity", errors.Keys);
    }
}
