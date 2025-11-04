using System.Threading.Tasks;
using Testcontainers.Kafka;
using Xunit;

public sealed class KafkaContainerTests : IAsyncLifetime
{
    private readonly KafkaContainer _kafka = new KafkaBuilder().Build();

    public async Task InitializeAsync() => await _kafka.StartAsync();
    public async Task DisposeAsync() => await _kafka.DisposeAsync();

    [Fact]
    public async Task Kafka_starts_and_has_bootstrap_address()
    {
        var bootstrap = _kafka.GetBootstrapAddress();
        Assert.False(string.IsNullOrWhiteSpace(bootstrap));
        await Task.CompletedTask;
    }
}
