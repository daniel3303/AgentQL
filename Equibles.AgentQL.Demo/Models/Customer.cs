using System.ComponentModel.DataAnnotations;
using Equibles.AgentQL.Attributes;

namespace Equibles.AgentQL.Demo.Models;

[AgentQLEntity(Description = "Travel agency customers with loyalty program tiers")]
public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(200)]
    public string Email { get; set; }

    [MaxLength(20)]
    public string Phone { get; set; }

    public LoyaltyTier LoyaltyTier { get; set; }

    public List<Booking> Bookings { get; set; } = [];
}
