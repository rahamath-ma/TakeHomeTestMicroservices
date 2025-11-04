using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Contracts.Events;

namespace OrderService.Messaging;

public sealed class UserCreatedConsumer : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<UserCreatedConsumer> _logger;
    private readonly string _bootstrap;
    private readonly IUserCache _userCache;

    public UserCreatedConsumer(
        IServiceProvider sp,
        ILogger<UserCreatedConsumer> logger,
        IConfiguration cfg,
        IUserCache userCache)
    {
        _sp = sp;
        _logger = logger;
        _bootstrap = cfg["Kafka:BootstrapServers"] ?? "kafka:9092";
        _userCache = userCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = new ConsumerConfig
        {
            BootstrapServers = _bootstrap,           // "kafka:9092" inside docker network
            GroupId = "orderservice-users-v4", 
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true,
            SocketKeepaliveEnable = true,
            BrokerAddressFamily = BrokerAddressFamily.V4 // to avoid IPv6 issues in containers
        };

        using var consumer = new ConsumerBuilder<string, string>(cfg)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe("users.created");
        _logger.LogInformation("UserCreatedConsumer listening on 'users.created'");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(TimeSpan.FromMilliseconds(250));
                    if (cr?.Message == null)
                    {
                        await Task.Delay(50, stoppingToken);
                        continue;
                    }

                    
                    var raw = cr.Message.Value;
                    _logger.LogInformation("RAW users.created payload: {Raw}", raw);

                    // unwrap if double-encoded: "\"{...}\""
                    if (!string.IsNullOrEmpty(raw) && raw[0] == '"')
                    {
                        raw = JsonSerializer.Deserialize<string>(raw)!;
                    }

                    var evt = JsonSerializer.Deserialize<UserCreatedEvent>(
                        raw,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (evt is null)
                    {
                        _logger.LogWarning("Failed to deserialize users.created payload");
                        continue;
                    }

                    _userCache.MarkKnown(evt.Id);
                    _logger.LogInformation("Consumed UserCreated for {UserId}", evt.Id);
                }
                catch (ConsumeException ce)
                {
                    _logger.LogWarning(ce, "ConsumeException in UserCreatedConsumer");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in UserCreatedConsumer loop");
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
