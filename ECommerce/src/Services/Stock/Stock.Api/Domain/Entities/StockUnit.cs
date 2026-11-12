namespace Stock.Api.Domain.Entities;

/// <summary>Stoktaki her fiziksel birim bir satırdır; ReservationId null ise birim müsaittir.</summary>
public sealed class StockUnit
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ReservationId { get; set; }
}
