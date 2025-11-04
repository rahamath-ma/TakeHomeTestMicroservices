namespace Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public DateTime? PublishedUtc { get; set; }   
    public int Attempts { get; set; }
    public string? Error { get; set; }
}
