using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

public class OrdersDb : DbContext
{
    public OrdersDb(DbContextOptions<OrdersDb> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();   

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>().HasKey(x => x.Id);
        // unique index for idempotency
        b.Entity<Order>()
            .HasIndex(x => x.IdempotencyKey)
            .IsUnique();
        b.Entity<OutboxMessage>().HasKey(x => x.Id);               
    }
}
