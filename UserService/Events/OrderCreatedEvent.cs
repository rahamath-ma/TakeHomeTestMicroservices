
namespace Contracts.Events;

public sealed record OrderCreatedEvent(
    Guid Id,
    Guid UserId,
    string Product,
    int Quantity,
    decimal Price,
    DateTime OccurredAtUtc
);
