using Stock.Api.Endpoints.Requests;

namespace Stock.Api.Endpoints.Validators;

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
