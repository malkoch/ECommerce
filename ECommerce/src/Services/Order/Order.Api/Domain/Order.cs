namespace Order.Api.Domain;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order() { }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items;

    public static Order Create(Guid customerId, IEnumerable<(Guid ProductId, int Quantity)> items)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.PendingStock,
            CreatedAtUtc = DateTime.UtcNow
        };
        order.UpdatedAtUtc = order.CreatedAtUtc;

        foreach (var (productId, quantity) in items)
        {
            if (productId == Guid.Empty || quantity <= 0)
            {
                throw new ArgumentException("Each item needs a ProductId and a positive Quantity.", nameof(items));
            }

            var existing = order._items.FindIndex(i => i.ProductId == productId);
            if (existing >= 0)
            {
                // Aynı ürün tekrar gelirse tek kalemde birleştirilir.
                var merged = new OrderItem(productId, order._items[existing].Quantity + quantity);
                order._items[existing] = merged;
            }
            else
            {
                order._items.Add(new OrderItem(productId, quantity));
            }
        }

        if (order._items.Count == 0)
        {
            throw new ArgumentException("An order needs at least one item.", nameof(items));
        }

        return order;
    }

    /// <summary>Yalnızca PendingStock'tan geçiş yapar; tekrar eden olay nihai durumu bozmaz. Değişiklik olduysa true döner.</summary>
    public bool Confirm() => Transition(OrderStatus.Confirmed);

    public bool Reject() => Transition(OrderStatus.Rejected);

    private bool Transition(OrderStatus target)
    {
        if (Status != OrderStatus.PendingStock)
        {
            return false;
        }

        Status = target;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
}
