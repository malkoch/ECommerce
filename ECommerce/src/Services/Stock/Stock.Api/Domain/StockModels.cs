namespace Stock.Api.Domain;

public sealed class StockItem
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
}

/// <summary>Stoktaki her fiziksel birim bir satırdır; ReservationId null ise birim müsaittir.</summary>
public sealed class StockUnit
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ReservationId { get; set; }
}

public static class ReservationStatus
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public sealed class StockReservation
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string Status { get; set; } = ReservationStatus.Pending;
    public string? ReasonCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StockReservationItem
{
    public Guid ReservationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }
    public string EventType { get; set; } = default!;
    public DateTime ProcessedAtUtc { get; set; }
}
