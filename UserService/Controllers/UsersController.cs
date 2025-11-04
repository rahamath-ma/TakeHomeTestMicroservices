using Infrastructure.Outbox;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Domain;
using Contracts.Events;

namespace UserService.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly UsersDb _db;
    private readonly IOutboxDispatcher _outbox;
    private readonly ILogger<UsersController> _logger;

    public UsersController(UsersDb db, IOutboxDispatcher outbox, ILogger<UsersController> logger)
    { _db = db; _outbox = outbox; _logger = logger; }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<User>> Create([FromBody] User input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (await _db.Users.AnyAsync(u => u.Email == input.Email, ct))
            return Conflict(new { message = "Email already exists" });

        _db.Users.Add(input);
        await _db.SaveChangesAsync(ct);

        var evt = new UserCreatedEvent
        {
            Id = input.Id,
            Name = input.Name,
            Email = input.Email,
            OccurredAtUtc = DateTime.UtcNow
        };

        await _outbox.EnqueueAsync("users.created", evt, ct);
        return CreatedAtAction(nameof(GetById), new { id = input.Id }, input);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    public async Task<ActionResult<User>> GetById(Guid id, CancellationToken ct)
        => await _db.Users.FindAsync(new object?[] { id }, ct) is { } u ? Ok(u) : NotFound();
}
