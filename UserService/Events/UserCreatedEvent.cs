namespace Contracts.Events;

public class UserCreatedEvent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public DateTime OccurredAtUtc { get; set; }
}
