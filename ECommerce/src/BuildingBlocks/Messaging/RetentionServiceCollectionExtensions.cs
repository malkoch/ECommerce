using Messaging.Options;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Messaging;

public static class RetentionServiceCollectionExtensions
{
    public static IServiceCollection AddRetentionCleanup<TContext>(this IServiceCollection services, bool hasInbox)
        where TContext : DbContext, IOutboxDbContext =>
        services.AddHostedService(sp => new RetentionCleanupService<TContext>(
            sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IOptions<RetentionOptions>>(),
            sp.GetRequiredService<ILogger<RetentionCleanupService<TContext>>>(), hasInbox));
}
