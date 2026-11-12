using Microsoft.Extensions.Options;
using Notification.Api.Exceptions;
using Notification.Api.Options;

namespace Notification.Api.Senders;

public sealed class LoggingEmailSender(IOptionsMonitor<MockProviderOptions> options, ILogger<LoggingEmailSender> logger)
    : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (options.CurrentValue.FailEmail)
        {
            throw new ProviderException("Mock email provider is configured to fail.");
        }

        logger.LogInformation("[MOCK EMAIL] to={To} subject={Subject}", to, subject);
        return Task.CompletedTask;
    }
}
