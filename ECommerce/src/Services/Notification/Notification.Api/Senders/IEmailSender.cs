namespace Notification.Api.Senders;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}
