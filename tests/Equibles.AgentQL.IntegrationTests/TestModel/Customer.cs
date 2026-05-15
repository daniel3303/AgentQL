using System.ComponentModel.DataAnnotations;
using Equibles.AgentQL.Attributes;

namespace Equibles.AgentQL.IntegrationTests.TestModel;

public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = default!;

    public LoyaltyTier Tier { get; set; }

    [AgentQLIgnore]
    [MaxLength(500)]
    public string? InternalNotes { get; set; }

    public List<Booking> Bookings { get; set; } = [];
}
