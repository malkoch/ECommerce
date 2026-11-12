namespace Notification.Api.Options;

public sealed class RetryOptions
{
    public const string SectionName = "Notification:Retry";

    public int MaxAttempts { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 500;
}
