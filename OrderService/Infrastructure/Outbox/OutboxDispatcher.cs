using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Messaging;
using Shared; // IKafkaProducer

namespace Infrastructure.Outbox;

public interface IOutboxDispatcher
{
    Task EnqueueAsync(string topic, object payload, CancellationToken ct = default);
}

public sealed class OutboxDispatcher<TDbContext> : BackgroundService, IOutboxDispatcher
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher<TDbContext>> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory,
                            ILogger<OutboxDispatcher<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Enqueue is called by controllers/services inside the request flow
    public async Task EnqueueAsync(string topic, object payload, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var msg = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Topic = topic,
            Payload = JsonSerializer.Serialize(payload), // serialize ONCE
            CreatedUtc = DateTime.UtcNow,
            Attempts = 0
        };

        db.Set<OutboxMessage>().Add(msg);
        await db.SaveChangesAsync(ct);
    }

    // Background loop that dispatches pending messages
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatcher<{Db}> started", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

                var batch = await db.Set<OutboxMessage>()
                    .Where(m => m.PublishedUtc == null && m.Attempts < 5)
                    .OrderBy(m => m.CreatedUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                if (batch.Count == 0)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                foreach (var m in batch)
                {
                    try
                    {
                  
                        await producer.ProduceAsync(m.Topic, m.Payload, m.Id.ToString());
                        
                        m.PublishedUtc = DateTime.UtcNow;
                        m.Error = null;
                        m.Attempts += 1;
                        _logger.LogInformation("Outbox published {Topic} for {MessageId}", m.Topic, m.Id);
                    }
                    catch (Exception ex)
                    {
                        m.Attempts += 1;
                        m.Error = ex.Message;
                        _logger.LogWarning(ex, "Outbox publish failed (Attempt {Attempts})", m.Attempts);
                    }
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch iteration failed");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("OutboxDispatcher<{Db}> stopped", typeof(TDbContext).Name);
    }
}
