using Messaging;
using Observability;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Stock.Api.Endpoints;
using Stock.Api.Infrastructure;
using Stock.Api.Handlers;

var builder = WebApplication.CreateBuilder(args);
builder.AddJsonLogging();

builder.Services.AddDbContext<StockDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("StockDb")));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<ReserveStockHandler>();
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<OutboxPublisher<StockDbContext>>();
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddRetentionCleanup<StockDbContext>(hasInbox: true);
builder.Services.AddReadinessChecks<StockDbContext>();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<StockDbContext>().Database.MigrateAsync();
}

app.UseCorrelationId("stock-api");
app.MapServiceHealth();
app.MapStockEndpoints();

app.Run();

public partial class Program;
