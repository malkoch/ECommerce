using Microsoft.EntityFrameworkCore;
using Npgsql;
using Order.Api.Domain;
using Order.Api.Infrastructure;
using OrderEntity = Order.Api.Domain.Entities.Order;

namespace Order.IntegrationTests;

/// <summary>
/// Geçici bir PostgreSQL database'i oluşturur, migration'ları uygular ve sonunda siler.
/// Admin bağlantısı TEST_POSTGRES_ADMIN ile verilir (örn. "Host=localhost;Username=postgres;Password=...").
/// </summary>
public sealed class OrderDatabaseFixture : IAsyncLifetime
{
    private readonly string _dbName = $"OrderDb_test_{Guid.NewGuid():N}";
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

    public OrderDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task<OrderEntity> AddOrderAsync()
    {
        await using var db = CreateContext();
        var order = OrderEntity.Create(Guid.NewGuid(), [(Guid.NewGuid(), 1)]);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public async Task<OrderStatus> GetStatusAsync(Guid orderId)
    {
        await using var db = CreateContext();
        return await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
    }

    public async Task<List<string>> OutboxRoutingKeysAsync(Guid orderId)
    {
        await using var db = CreateContext();
        var messages = await db.OutboxMessages.AsNoTracking().ToListAsync();
        return messages.Where(m => m.Payload.Contains(orderId.ToString())).Select(m => m.RoutingKey).ToList();
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
