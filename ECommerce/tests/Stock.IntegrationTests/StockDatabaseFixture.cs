using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stock.Api.Domain;
using Stock.Api.Infrastructure;

namespace Stock.IntegrationTests;

/// <summary>
/// Geçici bir PostgreSQL database'i oluşturur, migration'ları uygular ve sonunda siler.
/// Admin bağlantısı TEST_POSTGRES_ADMIN (örn. "Host=localhost;Username=postgres;Password=...") ile verilir.
/// </summary>
public sealed class StockDatabaseFixture : IAsyncLifetime
{
    private readonly string _dbName = $"StockDb_test_{Guid.NewGuid():N}";
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

    public StockDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<StockDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task<Guid> AddProductAsync(int quantity)
    {
        await using var db = CreateContext();
        var id = Guid.NewGuid();
        db.StockItems.Add(new StockItem { ProductId = id, Name = "Test" });
        db.StockUnits.AddRange(Enumerable.Range(0, quantity).Select(_ => new StockUnit { Id = Guid.NewGuid(), ProductId = id }));
        await db.SaveChangesAsync();
        return id;
    }

    public async Task<int> GetQuantityAsync(Guid productId)
    {
        await using var db = CreateContext();
        return await db.StockUnits.CountAsync(u => u.ProductId == productId && u.ReservationId == null);
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
