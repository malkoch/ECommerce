using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Messaging.Outbox;

namespace Messaging;

public sealed class RetentionOptions
{
    public const string SectionName = "Messaging:Retention";

    /// <summary>İşlenmiş outbox ve inbox kayıtlarının saklanma süresi. Inbox bu süreden sonra silindiği için bu süre, broker'ın tekrar teslim penceresinden uzun olmalıdır.</summary>
    public int RetentionDays { get; set; } = 7;
    public int IntervalMinutes { get; set; } = 60;
}

public static class RetentionServiceCollectionExtensions
{
    public static IServiceCollection AddRetentionCleanup<TContext>(this IServiceCollection services, bool hasInbox)
        where TContext : DbContext, IOutboxDbContext =>
        services.AddHostedService(sp => new RetentionCleanupService<TContext>(
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IOptions<RetentionOptions>>(),
            sp.GetRequiredService<ILogger<RetentionCleanupService<TContext>>>(), hasInbox));
}

/// <summary>İşlenmiş outbox kayıtlarını (ve varsa `inbox_messages` tablosunu) saklama süresi dolunca temizler.</summary>
public sealed class RetentionCleanupService<TContext>(
    IServiceScopeFactory scopes, IOptions<RetentionOptions> options,
    ILogger<RetentionCleanupService<TContext>> logger, bool hasInbox)
    : BackgroundService where TContext : DbContext, IOutboxDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retention cleanup failed; will retry on next interval");
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)), stoppingToken)
                .ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    public async Task<(int Outbox, int Inbox)> CleanAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.RetentionDays);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var outbox = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc != null && m.ProcessedAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);
        var inbox = hasInbox
            ? await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inbox_messages WHERE processed_at_utc < {cutoff}", ct)
            : 0;

        if (outbox + inbox > 0)
        {
            logger.LogInformation("Retention cleanup removed {Outbox} outbox and {Inbox} inbox rows older than {Cutoff:o}",
                outbox, inbox, cutoff);
        }

        return (outbox, inbox);
    }
}
