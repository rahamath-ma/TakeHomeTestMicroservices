
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UserService.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Outbox;

public interface IOutboxDispatcher
{
    Task EnqueueAsync(string topic, object evt, CancellationToken ct = default);
}

public class OutboxDispatcher<TDbContext> : BackgroundService, IOutboxDispatcher where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher<TDbContext>> _logger;
    private readonly Func<IServiceProvider, Task> _ensureDb;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _ensureDb = async sp =>
        {
            var db = sp.GetRequiredService<TDbContext>();
            await db.Database.EnsureCreatedAsync();
        };
    }

    public async Task EnqueueAsync(string topic, object evt, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await _ensureDb(scope.ServiceProvider);
        var payload = JsonSerializer.Serialize(evt);
        db.Add(new OutboxMessage { Topic = topic, Payload = payload });
        await db.SaveChangesAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                await _ensureDb(scope.ServiceProvider);

                var unsent = await db.Set<OutboxMessage>()
                    .Where(m => m.PublishedUtc == null)
                    .OrderBy(m => m.CreatedUtc)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                if (unsent.Count > 0)
                {
                    var producer = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();
                    foreach (var m in unsent)
                    {
                        await producer.ProduceAsync(m.Topic, m.Payload, key: m.Id.ToString(), ct: stoppingToken);
                        m.PublishedUtc = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
