
using System.ComponentModel.DataAnnotations;

namespace OrderService.Domain;

public class Order
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public Guid UserId { get; set; }

    [Required, StringLength(200)]
    public string Product { get; set; } = default!;

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    //  idempotency key  or server-generated fallback
    public string? IdempotencyKey { get; set; }
}
