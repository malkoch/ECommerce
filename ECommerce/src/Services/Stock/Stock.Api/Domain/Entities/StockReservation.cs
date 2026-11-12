using Stock.Api.Domain.Constants;

namespace Stock.Api.Domain.Entities;

public sealed class StockReservation
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = ReservationStatus.Pending;
    public string? ReasonCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
