using Microsoft.Extensions.Options;
using Notification.Api.Exceptions;
using Notification.Api.Options;

namespace Notification.Api.Senders;

public sealed class LoggingSmsSender(IOptionsMonitor<MockProviderOptions> options, ILogger<LoggingSmsSender> logger)
    : ISmsSender
{
    public Task SendAsync(string to, string text, CancellationToken ct)
    {
        if (options.CurrentValue.FailSms)
        {
            throw new ProviderException("Mock SMS provider is configured to fail.");
        }

        logger.LogInformation("[MOCK SMS] to={To}", to);
        return Task.CompletedTask;
    }
}
