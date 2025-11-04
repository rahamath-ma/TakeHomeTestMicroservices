using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Contracts.Events;

namespace UserService.Messaging;

public sealed class OrderCreatedConsumer : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly string _bootstrap;

    public OrderCreatedConsumer(IServiceProvider sp, ILogger<OrderCreatedConsumer> logger, IConfiguration cfg)
    {
        _sp = sp;
        _logger = logger;
        _bootstrap = cfg["Kafka:BootstrapServers"] ?? "kafka:9092";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = new ConsumerConfig
        {
            BootstrapServers = _bootstrap,
            GroupId = "userservice-orders",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true,
            SocketKeepaliveEnable = true,
            // avoid IPv6 surprises in containers
            BrokerAddressFamily = BrokerAddressFamily.V4
        };

        using var consumer = new ConsumerBuilder<string, string>(cfg)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe("orders.created");
        _logger.LogInformation("OrderCreatedConsumer listening on 'orders.created'");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //keep loop responsive
                    var cr = consumer.Consume(TimeSpan.FromMilliseconds(250));
                    if (cr?.Message == null)
                    {
                        await Task.Delay(50, stoppingToken);
                        continue;
                    }

                    var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(cr.Message.Value);
                    if (evt is null) continue;

                    // DB usage but shows the pattern)
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<UsersDb>();
                    // project something to UsersDb here if desired.

                    _logger.LogInformation("Consumed OrderCreated: {OrderId} for {UserId}", evt.Id, evt.UserId);
                }
                catch (ConsumeException ce)
                {
                    _logger.LogWarning(ce, "ConsumeException in OrderCreatedConsumer");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    
                    _logger.LogError(ex, "Unhandled exception in consumer loop");
                    await Task.Delay(500, stoppingToken);
                }
            }
        }
        finally
        {
            try { consumer.Close(); } catch { /* ignore */ }
        }
    }
}
