using Microsoft.EntityFrameworkCore;

namespace Messaging.Outbox;

public static class OutboxModelBuilderExtensions
{
    public static void ConfigureOutbox(this ModelBuilder b) =>
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(100);
            e.Property(x => x.RoutingKey).HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.HasIndex(x => x.ProcessedAtUtc).HasFilter("\"ProcessedAtUtc\" IS NULL");
        });

    public static OutboxMessage CreateOutboxMessage<T>(EventEnvelope<T> envelope, string routingKey) => new()
    {
        Id = envelope.MessageId,
        CorrelationId = envelope.CorrelationId,
        EventType = envelope.EventType,
        RoutingKey = routingKey,
        Payload = System.Text.Json.JsonSerializer.Serialize(envelope, System.Text.Json.JsonSerializerOptions.Web),
        CreatedAtUtc = envelope.OccurredAtUtc
    };
}
