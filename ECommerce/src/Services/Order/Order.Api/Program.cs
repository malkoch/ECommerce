using Messaging;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Order.Api.Endpoints;
using Order.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisher<OrderDbContext>>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
}

app.MapHealthChecks("/health/live");
app.MapOrderEndpoints();

app.Run();

public partial class Program;
