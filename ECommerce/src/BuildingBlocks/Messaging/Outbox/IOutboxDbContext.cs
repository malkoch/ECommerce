using Microsoft.EntityFrameworkCore;

namespace Messaging.Outbox;

public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
}
