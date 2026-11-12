using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging.Outbox;

public sealed class OutboxPublisher<TContext>(
    IServiceScopeFactory scopes, IMessagePublisher publisher, ILogger<OutboxPublisher<TContext>> logger)
    : BackgroundService where TContext : DbContext, IOutboxDbContext
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = PollInterval;
            try
            {
                if (await PublishPendingAsync(stoppingToken) == BatchSize)
                {
                    delay = TimeSpan.Zero;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox publish failed; will retry in {Delay}s", ErrorBackoff.TotalSeconds);
                delay = ErrorBackoff;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
            }
        }
    }

    private async Task<int> PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        // Row locks are held until commit, so concurrent publishers skip rows claimed by another instance.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var batch = await db.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE "ProcessedAtUtc" IS NULL
                ORDER BY "CreatedAtUtc"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in batch)
        {
            message.Attempts++;
            try
            {
                await publisher.PublishAsync(message.RoutingKey, message.Id, message.CorrelationId, message.EventType,
                    message.Payload, ct);
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
                logger.LogInformation("Outbox message published {MessageId} {EventType} {CorrelationId}",
                    message.Id, message.EventType, message.CorrelationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);
                await tx.CommitAsync(CancellationToken.None);
                throw;
            }

            await db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return batch.Count;
    }
}
