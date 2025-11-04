using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using UserService.Domain;

public class UsersDb : DbContext
{
    public UsersDb(DbContextOptions<UsersDb> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();   

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasKey(x => x.Id);
        b.Entity<OutboxMessage>().HasKey(x => x.Id);               
    }
}
