using System.Text.Json;

namespace Messaging;

public static class MessageRetry
{
    /// <summary>
    /// Geçici hatalarda üstel backoff ile en fazla <paramref name="maxAttempts"/> kez dener.
    /// Kalıcı hatalar (<see cref="PermanentMessageException"/>, <see cref="JsonException"/>) ve iptal tekrar denenmez.
    /// Son denemenin hatası çağırana fırlatılır.
    /// </summary>
    public static async Task ExecuteAsync(
        Func<int, Task> action, int maxAttempts, TimeSpan baseDelay, Action<int, Exception>? onRetry, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action(attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                onRetry?.Invoke(attempt, ex);
                if (baseDelay > TimeSpan.Zero)
                {
                    await Task.Delay(TimeSpan.FromTicks(baseDelay.Ticks * (1L << (attempt - 1))), ct);
                }
            }
        }
    }

    public static bool IsTransient(Exception ex) =>
        ex is not (OperationCanceledException or PermanentMessageException or JsonException);
}
