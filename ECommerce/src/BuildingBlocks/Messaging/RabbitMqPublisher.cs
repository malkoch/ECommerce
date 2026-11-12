using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Messaging;

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(string routingKey, Guid messageId, Guid correlationId, string eventType, string json, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            var props = new BasicProperties
            {
                MessageId = messageId.ToString(),
                CorrelationId = correlationId.ToString(),
                Type = eventType,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };
            // Publisher confirms açık kanalda bu çağrı broker onayı gelene kadar döner; hata fırlatırsa onay alınmamıştır.
            await channel.BasicPublishAsync(Topology.Exchange, routingKey, mandatory: false, props,
                Encoding.UTF8.GetBytes(json), ct);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await ResetAsync();
        var o = options.Value;
        var factory = new ConnectionFactory { HostName = o.Host, Port = o.Port, UserName = o.User, Password = o.Password };
        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await Topology.DeclareAsync(_channel, ct);
        return _channel;
    }

    private async Task ResetAsync()
    {
        try { if (_channel is not null) await _channel.DisposeAsync(); } catch { /* bağlantı zaten kopmuş olabilir */ }
        try { if (_connection is not null) await _connection.DisposeAsync(); } catch { }
        _channel = null;
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _gate.Dispose();
    }
}
