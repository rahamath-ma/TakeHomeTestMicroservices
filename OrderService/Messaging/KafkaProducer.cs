
using Confluent.Kafka;
using System.Text.Json;

namespace OrderService.Messaging;

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, T message, string? key = null, IDictionary<string,string>? headers = null, CancellationToken ct = default);
}

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(IConfiguration cfg)
    {
        var bootstrap = cfg["Kafka:BootstrapServers"] ?? "localhost:9092";
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrap,
            ClientId = "order-service"
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync<T>(string topic, T message, string? key = null, IDictionary<string,string>? headers = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(message);
        var msg = new Message<string, string> { Key = key ?? Guid.NewGuid().ToString(), Value = json };
        if (headers != null && headers.Count > 0)
        {
            msg.Headers = new Headers();
            foreach (var kv in headers) msg.Headers.Add(kv.Key, System.Text.Encoding.UTF8.GetBytes(kv.Value));
        }
        await _producer.ProduceAsync(topic, msg, ct);
    }

    public void Dispose() => _producer?.Dispose();
}
