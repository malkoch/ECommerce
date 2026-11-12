using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging;

public sealed class RabbitMqHealthCheck(IOptions<RabbitMqOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var o = options.Value;
        try
        {
            var factory = new ConnectionFactory { HostName = o.Host, Port = o.Port, UserName = o.User, Password = o.Password };
            await using var connection = await factory.CreateConnectionAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is not reachable.", ex);
        }
    }
}
