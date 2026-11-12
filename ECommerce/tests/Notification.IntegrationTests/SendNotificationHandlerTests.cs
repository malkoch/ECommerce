using Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notification.Api.Domain;
using Notification.Api.Handlers;

namespace Notification.IntegrationTests;

public class SendNotificationHandlerTests(NotificationDatabaseFixture fixture) : IClassFixture<NotificationDatabaseFixture>
{
    private static OrderResultNotification Confirmed() =>
        new(Guid.NewGuid(), Guid.NewGuid(), EventTypes.OrderConfirmed, Guid.NewGuid(), Guid.NewGuid(), null);

    private Task HandleAsync(OrderResultNotification n, FakeSender sender, int maxAttempts = 3)
    {
        var db = fixture.CreateContext();
        var handler = new SendNotificationHandler(db, sender, sender,
            Options.Create(new RetryOptions { MaxAttempts = maxAttempts, BaseDelayMs = 0 }),
            NullLogger<SendNotificationHandler>.Instance);
        return handler.HandleAsync(n, CancellationToken.None).ContinueWith(t => { db.Dispose(); return t; }).Unwrap();
    }

    [Fact]
    public async Task Confirmed_SendsEmailAndSmsAndLogsThem()
    {
        var sender = new FakeSender();
        var n = Confirmed();

        await HandleAsync(n, sender);

        var rows = await fixture.GetAsync(n.MessageId);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(NotificationStatus.Sent, r.Status);
            Assert.NotNull(r.SentAtUtc);
            Assert.Equal(n.OrderId, r.OrderId);
        });
        Assert.Equal((1, 1), (sender.EmailCalls, sender.SmsCalls));
    }

    [Fact]
    public async Task Rejected_BodyContainsReason()
    {
        var n = Confirmed() with { EventType = EventTypes.OrderRejected, ReasonCode = "INSUFFICIENT_STOCK" };

        await HandleAsync(n, new FakeSender());

        Assert.All(await fixture.GetAsync(n.MessageId), r => Assert.Contains("yetersiz stok", r.Body));
    }

    [Fact]
    public async Task SameMessageTwice_SendsOnce()
    {
        var sender = new FakeSender();
        var n = Confirmed();

        await HandleAsync(n, sender);
        await HandleAsync(n, sender);

        Assert.Equal((1, 1), (sender.EmailCalls, sender.SmsCalls));
        Assert.Equal(2, (await fixture.GetAsync(n.MessageId)).Count);
    }

    [Fact]
    public async Task TransientProviderFailure_IsRetriedAndSucceeds()
    {
        var sender = new FakeSender { FailEmailTimes = 2 };
        var n = Confirmed();

        await HandleAsync(n, sender);

        var email = (await fixture.GetAsync(n.MessageId)).Single(r => r.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationStatus.Sent, email.Status);
        Assert.Equal(3, email.Attempts);
        Assert.Equal(3, sender.EmailCalls);
    }

    [Fact]
    public async Task PermanentFailure_MarksFailedAndThrows_ThenReplayRetriesOnlyFailedChannel()
    {
        var sender = new FakeSender { FailSmsAlways = true };
        var n = Confirmed();

        await Assert.ThrowsAsync<NotificationDeliveryException>(() => HandleAsync(n, sender));

        var rows = await fixture.GetAsync(n.MessageId);
        Assert.Equal(NotificationStatus.Sent, rows.Single(r => r.Channel == NotificationChannel.Email).Status);
        var sms = rows.Single(r => r.Channel == NotificationChannel.Sms);
        Assert.Equal(NotificationStatus.Failed, sms.Status);
        Assert.Equal(3, sms.Attempts);
        Assert.Equal("sms down", sms.LastError);

        sender.FailSmsAlways = false;
        await HandleAsync(n, sender);

        rows = await fixture.GetAsync(n.MessageId);
        Assert.All(rows, r => Assert.Equal(NotificationStatus.Sent, r.Status));
        Assert.Equal(1, sender.EmailCalls);
        Assert.Equal(4, sender.SmsCalls);
    }

    [Fact]
    public async Task ConcurrentDelivery_SendsEachChannelOnce()
    {
        var sender = new FakeSender();
        var n = Confirmed();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => HandleAsync(n, sender))));

        Assert.Equal((1, 1), (sender.EmailCalls, sender.SmsCalls));
        Assert.All(await fixture.GetAsync(n.MessageId), r => Assert.Equal(NotificationStatus.Sent, r.Status));
    }
}
