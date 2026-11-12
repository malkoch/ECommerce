using Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Observability;

public static class ObservabilityExtensions
{
    public const string CorrelationHeader = "X-Correlation-Id";
    private const string ReadyTag = "ready";

    /// <summary>Yapılandırılmış JSON console log; scope alanları (CorrelationId, MessageId, ServiceName...) çıktıya dahil edilir.</summary>
    public static WebApplicationBuilder AddJsonLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });
        return builder;
    }

    /// <summary>`/health/ready` için veritabanı ve RabbitMQ kontrollerini kaydeder.</summary>
    public static IHealthChecksBuilder AddReadinessChecks<TContext>(this IServiceCollection services) where TContext : DbContext =>
        services.AddHealthChecks()
            .AddDbContextCheck<TContext>("database", tags: [ReadyTag])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [ReadyTag]);

    /// <summary>`/health/live` yalnızca process canlılığını, `/health/ready` bağımlılıkları kontrol eder.</summary>
    public static IEndpointRouteBuilder MapServiceHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) });
        return app;
    }

    /// <summary>Gelen `X-Correlation-Id` başlığını (yoksa yenisini) istek boyunca log scope'una ve yanıta taşır.</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app, string serviceName) =>
        app.Use(async (context, next) =>
        {
            if (!Guid.TryParse(context.Request.Headers[CorrelationHeader], out var correlationId))
            {
                correlationId = Guid.NewGuid();
                context.Request.Headers[CorrelationHeader] = correlationId.ToString();
            }

            context.Response.Headers[CorrelationHeader] = correlationId.ToString();

            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Correlation");
            using (logger.BeginScope(new Dictionary<string, object?>
                   {
                       ["ServiceName"] = serviceName,
                       ["CorrelationId"] = correlationId
                   }))
            {
                await next();
            }
        });
}
