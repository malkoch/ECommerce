namespace Messaging.Options;

public sealed class RetentionOptions
{
    public const string SectionName = "Messaging:Retention";

    /// <summary>İşlenmiş outbox ve inbox kayıtlarının saklanma süresi. Inbox bu süreden sonra silindiği için bu süre, broker'ın tekrar teslim penceresinden uzun olmalıdır.</summary>
    public int RetentionDays { get; set; } = 7;
    public int IntervalMinutes { get; set; } = 60;
}
