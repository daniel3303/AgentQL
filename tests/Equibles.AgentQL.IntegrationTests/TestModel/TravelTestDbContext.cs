using Microsoft.EntityFrameworkCore;

namespace Equibles.AgentQL.IntegrationTests.TestModel;

public class TravelTestDbContext : DbContext
{
    public TravelTestDbContext(DbContextOptions<TravelTestDbContext> options)
        : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Booking> Bookings => Set<Booking>();
}
