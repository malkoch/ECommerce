using Stock.Api.Infrastructure;

namespace Stock.Api.Endpoints;

public sealed record CreateProductRequest(string? Name);

public sealed record AddStockRequest(int Quantity);

public sealed record StockLevelResponse(Guid ProductId, string Name, int Available, int Reserved, int Total);

public static class StockRequestValidator
{
    public const int MaxNameLength = 200;
    public const int MaxQuantityPerRequest = 100_000;

    public static Dictionary<string, string[]> Validate(CreateProductRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
        }
        else if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (request.Name.Trim().Length > MaxNameLength)
        {
            errors["name"] = [$"Name must be at most {MaxNameLength} characters."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(AddStockRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
        }
        else if (request.Quantity <= 0 || request.Quantity > MaxQuantityPerRequest)
        {
            errors["quantity"] = [$"Quantity must be between 1 and {MaxQuantityPerRequest}."];
        }

        return errors;
    }
}

public static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products");

        group.MapPost("/", async (CreateProductRequest? request, IStockRepository repository, CancellationToken ct) =>
        {
            var errors = StockRequestValidator.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var id = Guid.NewGuid();
            var name = request!.Name!.Trim();
            await repository.AddProductAsync(id, name, ct);
            return Results.Created($"/api/products/{id}/stock", new { productId = id, name });
        });

        group.MapPost("/{productId:guid}/stock", async (Guid productId, AddStockRequest? request, IStockRepository repository, CancellationToken ct) =>
        {
            var errors = StockRequestValidator.Validate(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var name = await repository.GetProductNameAsync(productId, ct);
            if (name is null)
            {
                return Results.NotFound();
            }

            await repository.AddUnitsAsync(productId, request!.Quantity, ct);
            return Results.Ok(await BuildLevelAsync(repository, productId, name, ct));
        });

        group.MapGet("/{productId:guid}/stock", async (Guid productId, IStockRepository repository, CancellationToken ct) =>
        {
            var name = await repository.GetProductNameAsync(productId, ct);
            return name is null
                ? Results.NotFound()
                : Results.Ok(await BuildLevelAsync(repository, productId, name, ct));
        });
    }

    private static async Task<StockLevelResponse> BuildLevelAsync(
        IStockRepository repository, Guid productId, string name, CancellationToken ct)
    {
        var (available, reserved) = await repository.GetStockCountsAsync(productId, ct);
        return new StockLevelResponse(productId, name, available, reserved, available + reserved);
    }
}
