namespace Stock.Api.Domain.Entities;

public sealed class StockItem
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
}
