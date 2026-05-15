using System.ComponentModel.DataAnnotations;
using Equibles.AgentQL.Attributes;

namespace Equibles.AgentQL.IntegrationTests.TestModel;

[AgentQLEntity(Description = "A trip booked by a customer.")]
public class Booking
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Destination { get; set; } = default!;

    public decimal Price { get; set; }

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = default!;
}
