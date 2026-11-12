using Order.Api.Endpoints.Requests;

namespace Order.Api.Endpoints.Validators;

public static class CreateOrderValidator
{
    public static Dictionary<string, string[]> Validate(CreateOrderRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["body"] = ["Request body is required."];
            return errors;
        }

        if (request.CustomerId == Guid.Empty)
        {
            errors["customerId"] = ["CustomerId is required."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors["items"] = ["At least one item is required."];
            return errors;
        }

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (item is null || item.ProductId == Guid.Empty)
            {
                errors[$"items[{i}].productId"] = ["ProductId is required."];
            }

            if (item is null || item.Quantity <= 0)
            {
                errors[$"items[{i}].quantity"] = ["Quantity must be greater than zero."];
            }
        }

        return errors;
    }
}
