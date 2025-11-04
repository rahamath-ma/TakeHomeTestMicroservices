using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Controllers;
//using OrderService.Infrastructure;
using OrderService.Messaging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Outbox;

public class OrderTests
{
    private static OrdersDb CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<OrdersDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OrdersDb(options);
    }

    [Fact]
    public async Task CannotCreateOrderForUnknownUser()
    {
        var db = CreateInMemoryDb();
        var outbox = new FakeOutbox();
        var cache = new FakeUserCache(false); // empty cache ⇒ unknown user
        var logger = NullLogger<OrdersController>.Instance;

        var controller = new OrdersController(db, outbox, cache, logger);

        var req = new CreateOrderRequest
        {
            UserId = Guid.NewGuid(),
            Product = "Book",
            Quantity = 1,
            Price = 10
        };

        var result = await controller.Create(req, null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }


    private sealed class FakeOutbox : IOutboxDispatcher
    {
        public readonly System.Collections.Generic.List<(string Topic, object Payload)> Published
            = new();

        public Task EnqueueAsync(string topic, object payload, CancellationToken ct = default)
        {
            Published.Add((topic, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserCache : IUserCache
    {
        private readonly bool _isKnown;
        public FakeUserCache(bool isKnown) => _isKnown = isKnown;

        public bool IsKnown(Guid id) => _isKnown;
        public void MarkKnown(Guid id) { /* no-op */ }
    }
}


