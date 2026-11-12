using System.Text.Json;
using Messaging;

namespace Messaging.UnitTests;

public class MessageRetryTests
{
    [Fact]
    public async Task Succeeds_after_transient_failures()
    {
        var calls = 0;
        await MessageRetry.ExecuteAsync(a =>
        {
            calls++;
            if (a < 3) throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        }, 3, TimeSpan.Zero, null, CancellationToken.None);

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Throws_last_error_after_max_attempts()
    {
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => MessageRetry.ExecuteAsync(
            _ => { calls++; throw new InvalidOperationException(); }, 3, TimeSpan.Zero, null, CancellationToken.None));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Permanent_error_is_not_retried()
    {
        var calls = 0;
        await Assert.ThrowsAsync<PermanentMessageException>(() => MessageRetry.ExecuteAsync(
            _ => { calls++; throw new PermanentMessageException("bad"); }, 3, TimeSpan.Zero, null, CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Json_error_is_not_retried()
    {
        var calls = 0;
        await Assert.ThrowsAsync<JsonException>(() => MessageRetry.ExecuteAsync(
            _ => { calls++; throw new JsonException(); }, 3, TimeSpan.Zero, null, CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OnRetry_is_called_for_each_retry()
    {
        var retries = new List<int>();
        await Assert.ThrowsAsync<Exception>(() => MessageRetry.ExecuteAsync(
            _ => throw new Exception(), 3, TimeSpan.Zero, (a, _) => retries.Add(a), CancellationToken.None));

        Assert.Equal([1, 2], retries);
    }
}
