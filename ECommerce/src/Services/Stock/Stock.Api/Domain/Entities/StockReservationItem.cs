namespace Stock.Api.Domain.Entities;

public sealed class StockReservationItem
{
    public Guid ReservationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
