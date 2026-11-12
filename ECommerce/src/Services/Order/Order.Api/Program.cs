using Messaging;
using Observability;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Api.Endpoints;
using Order.Api.Handlers;
using Order.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddJsonLogging();

builder.Services.AddDbContext<OrderDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisher<OrderDbContext>>();
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddRetentionCleanup<OrderDbContext>(hasInbox: true);
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<StockResultHandler>();
builder.Services.AddHostedService<StockResultConsumer>();
builder.Services.AddReadinessChecks<OrderDbContext>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "Order API", Version = "v1" }));

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Order API v1"));

app.UseCorrelationId("order-api");
app.MapServiceHealth();
app.MapOrderEndpoints();

app.Run();

public partial class Program;
