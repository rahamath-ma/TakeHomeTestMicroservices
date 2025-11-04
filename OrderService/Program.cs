using Serilog;
using Shared;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using OrderService.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Logging
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Core Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrdersDb>(opt => opt.UseInMemoryDatabase("orders"));
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

// Dependencies used by background services
builder.Services.AddSingleton<IUserCache, UserCache>();           // register cache first

// Background Services
builder.Services.AddHostedService<UserCreatedConsumer>();         // listens to "users.created"

// Outbox (enable once order posting succeeds)
builder.Services.AddSingleton<OutboxDispatcher<OrdersDb>>();
builder.Services.AddSingleton<IOutboxDispatcher>(sp => sp.GetRequiredService<OutboxDispatcher<OrdersDb>>());
// builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcher<OrdersDb>>()); // 

// Health checks
builder.Services.AddHealthChecks();

// Pipeline
var app = builder.Build();
app.UseCorrelationId();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
