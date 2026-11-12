namespace Order.Api.Domain.Entities;

public sealed class OrderItem
{
    private OrderItem() { }

    public OrderItem(Guid productId, int quantity)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
}
