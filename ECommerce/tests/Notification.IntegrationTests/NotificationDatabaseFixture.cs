using Microsoft.EntityFrameworkCore;
using Npgsql;
using Notification.Api.Domain;
using Notification.Api.Infrastructure;
using Notification.Api.Senders;

namespace Notification.IntegrationTests;

/// <summary>
/// Geçici bir PostgreSQL database'i oluşturur, migration'ları uygular ve sonunda siler.
/// Admin bağlantısı TEST_POSTGRES_ADMIN ile verilir (örn. "Host=localhost;Username=postgres;Password=...").
/// </summary>
public sealed class NotificationDatabaseFixture : IAsyncLifetime
{
    private readonly string _dbName = $"NotificationDb_test_{Guid.NewGuid():N}";
    private string _adminConnection = default!;

    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _adminConnection = Environment.GetEnvironmentVariable("TEST_POSTGRES_ADMIN")
            ?? throw new InvalidOperationException(
                "Set TEST_POSTGRES_ADMIN, e.g. \"Host=localhost;Username=postgres;Password=<admin password>\" (docker compose up -d must be running).");

        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", admin);
            await cmd.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _dbName }.ConnectionString;
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public NotificationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task<List<NotificationRecord>> GetAsync(Guid messageId)
    {
        await using var db = CreateContext();
        return await db.Notifications.AsNoTracking().Where(n => n.MessageId == messageId).OrderBy(n => n.Channel).ToListAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }
}

public sealed class FakeSender : IEmailSender, ISmsSender
{
    private int _emailCalls;
    private int _smsCalls;

    public int EmailCalls => _emailCalls;
    public int SmsCalls => _smsCalls;

    /// <summary>İlk N e-posta çağrısı hata verir.</summary>
    public int FailEmailTimes { get; set; }
    public bool FailSmsAlways { get; set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        if (Interlocked.Increment(ref _emailCalls) <= FailEmailTimes)
        {
            throw new ProviderException("email down");
        }

        return Task.CompletedTask;
    }

    public Task SendAsync(string to, string text, CancellationToken ct)
    {
        Interlocked.Increment(ref _smsCalls);
        return FailSmsAlways ? throw new ProviderException("sms down") : Task.CompletedTask;
    }
}
