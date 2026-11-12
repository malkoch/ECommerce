using Stock.Api.Endpoints;
using Stock.Api.Infrastructure;

namespace Stock.IntegrationTests;

public class StockEndpointsTests(StockDatabaseFixture fixture) : IClassFixture<StockDatabaseFixture>
{
    [Fact]
    public async Task AddProductAndStock_ReportsCounts()
    {
        var id = Guid.NewGuid();
        await using var db = fixture.CreateContext();
        var repo = new StockRepository(db);

        Assert.Null(await repo.GetProductNameAsync(id, default));
        await repo.AddProductAsync(id, "Mouse", default);
        await repo.AddUnitsAsync(id, 5, default);

        Assert.Equal("Mouse", await repo.GetProductNameAsync(id, default));
        Assert.Equal((5, 0), await repo.GetStockCountsAsync(id, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(StockRequestValidator.MaxQuantityPerRequest + 1)]
    public void InvalidQuantity_Fails(int quantity) =>
        Assert.Contains("quantity", StockRequestValidator.Validate(new AddStockRequest(quantity)).Keys);

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void BlankName_Fails(string? name) =>
        Assert.Contains("name", StockRequestValidator.Validate(new CreateProductRequest(name)).Keys);
}
