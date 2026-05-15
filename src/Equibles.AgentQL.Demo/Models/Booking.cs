using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Equibles.AgentQL.Attributes;

namespace Equibles.AgentQL.Demo.Models;

[AgentQLEntity(Description = "Customer bookings for travel destinations")]
public class Booking
{
    [Key]
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    public int DestinationId { get; set; }
    public Destination Destination { get; set; }

    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; }

    public List<Payment> Payments { get; set; } = [];
}
