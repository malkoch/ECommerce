namespace Stock.Api.Endpoints.Responses;

public sealed record StockLevelResponse(Guid ProductId, string Name, int Available, int Reserved, int Total);
