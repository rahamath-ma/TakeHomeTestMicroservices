using Serilog;
using Shared;
using Infrastructure.Outbox;              
using Microsoft.EntityFrameworkCore;
using UserService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------
// Logging (Serilog)
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ----------------------------------------------------
// Core Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<UsersDb>(opt => opt.UseInMemoryDatabase("users"));
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

// ----------------------------------------------------
// Background Services
// Consumer that listens to "orders.created"
builder.Services.AddHostedService<OrderCreatedConsumer>();

// Outbox pattern to reliably publish "users.created"
builder.Services.AddSingleton<OutboxDispatcher<UsersDb>>();
builder.Services.AddSingleton<IOutboxDispatcher>(sp => sp.GetRequiredService<OutboxDispatcher<UsersDb>>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcher<UsersDb>>());

// ----------------------------------------------------
// Health checks
builder.Services.AddHealthChecks();

// ----------------------------------------------------
// Middleware pipeline
var app = builder.Build();

app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
