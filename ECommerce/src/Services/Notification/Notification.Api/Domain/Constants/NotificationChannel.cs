namespace Notification.Api.Domain.Constants;

public static class NotificationChannel
{
    public const string Email = "Email";
    public const string Sms = "Sms";
    public static readonly string[] All = [Email, Sms];
}
