using Equibles.AgentQL.Demo.Models;
using Microsoft.EntityFrameworkCore;

namespace Equibles.AgentQL.Demo;

public class TravelDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Destination> Destinations { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }

    public TravelDbContext(DbContextOptions<TravelDbContext> options) : base(options)
    {
    }
}
