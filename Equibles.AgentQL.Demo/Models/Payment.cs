using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Equibles.AgentQL.Attributes;

namespace Equibles.AgentQL.Demo.Models;

[AgentQLEntity(Description = "Payments made for bookings")]
public class Payment
{
    [Key]
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public DateTime PaidAt { get; set; }
}
