using Microsoft.Extensions.Options;

namespace Notification.Api.Senders;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public interface ISmsSender
{
    Task SendAsync(string to, string text, CancellationToken ct);
}

public sealed class ProviderException(string message) : Exception(message);

public sealed class MockProviderOptions
{
    public const string SectionName = "Notification:Mock";

    /// <summary>Hata senaryosunu göstermek için ilgili kanalı hata verecek şekilde işaretler.</summary>
    public bool FailEmail { get; set; }
    public bool FailSms { get; set; }
}

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
