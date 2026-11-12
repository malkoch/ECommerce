namespace Notification.Api.Senders;

public interface ISmsSender
{
    Task SendAsync(string to, string text, CancellationToken ct);
}
