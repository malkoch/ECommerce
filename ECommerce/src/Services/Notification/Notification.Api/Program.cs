using Messaging;
using Observability;
using Microsoft.EntityFrameworkCore;
using Notification.Api.Handlers;
using Notification.Api.Infrastructure;
using Notification.Api.Senders;

var builder = WebApplication.CreateBuilder(args);
builder.AddJsonLogging();

builder.Services.AddDbContext<NotificationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("NotificationDb")));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<RetryOptions>(builder.Configuration.GetSection(RetryOptions.SectionName));
builder.Services.Configure<MockProviderOptions>(builder.Configuration.GetSection(MockProviderOptions.SectionName));
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<ISmsSender, LoggingSmsSender>();
builder.Services.AddScoped<SendNotificationHandler>();
builder.Services.AddHostedService<OrderEventsConsumer>();
builder.Services.AddReadinessChecks<NotificationDbContext>();

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
}

app.UseCorrelationId("notification-api");
app.MapServiceHealth();

app.Run();

public partial class Program;
