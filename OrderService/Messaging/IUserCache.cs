using System;
using System.Collections.Concurrent;

namespace OrderService.Messaging;

public interface IUserCache
{
    bool IsKnown(Guid userId);
    void MarkKnown(Guid userId);
}

public sealed class UserCache : IUserCache
{
    private readonly ConcurrentDictionary<Guid, byte> _known = new();

    public bool IsKnown(Guid userId) => _known.ContainsKey(userId);
    public void MarkKnown(Guid userId) => _known[userId] = 1;
}
