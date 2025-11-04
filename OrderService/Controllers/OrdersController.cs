using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Messaging;        // IUserCache
using Infrastructure.Outbox;        // IOutboxDispatcher
using OrderService.Domain;          // Order entity (adjust if your namespace differs)
using Contracts.Events;             // OrderCreatedEvent (contract)
using System.ComponentModel.DataAnnotations;

namespace OrderService.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly OrdersDb _db;
    private readonly IOutboxDispatcher _outbox;
    private readonly IUserCache _userCache;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        OrdersDb db,
        IOutboxDispatcher outbox,
        IUserCache userCache,
        ILogger<OrdersController> logger)
    {
        _db = db;
        _outbox = outbox;
        _userCache = userCache;
        _logger = logger;
    }

    // POST /orders
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest req, [FromHeader(Name = "Idempotency-Key")] string? idemKey, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Optional safety: require that the user was observed via Kafka
        if (!_userCache.IsKnown(req.UserId))
            return BadRequest($"Unknown user: {req.UserId}");
        // If client didn't send a key, compute a deterministic fallback
        if (string.IsNullOrWhiteSpace(idemKey))
            idemKey = $"{req.UserId}:{req.Product}:{req.Quantity}:{req.Price}";
        // Check if we already processed this key
        var existing = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idemKey, ct);
        if (existing != null)
        {
            // Either return 200 with existing order OR 409 Conflict
            // Returning 200/OK with the original resource is common for idempotent POSTs
            return Ok(existing);
        }
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = req.UserId,
            Product = req.Product,
            Quantity = req.Quantity,
            Price = req.Price,
            IdempotencyKey = idemKey
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Publish outbox event (processed by OutboxDispatcher)
        var evt = new OrderCreatedEvent(
     order.Id,
     order.UserId,
     order.Product,
     order.Quantity,
     order.Price,
     DateTime.UtcNow
 );
        await _outbox.EnqueueAsync("orders.created", evt, ct);

        _logger.LogInformation("Created order {OrderId} for user {UserId}", order.Id, order.UserId);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { order.Id });
    }

    // GET /orders/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();
        return Ok(order);
    }
}

// Request DTO (simple validation)
public sealed class CreateOrderRequest
{
    [Required] public Guid UserId { get; set; }
    [Required] public string Product { get; set; } = default!;
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    [Range(typeof(decimal), "0.0", "79228162514264337593543950335")]
    public decimal Price { get; set; }
}
