using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Equibles.AgentQL.Attributes;

namespace Equibles.AgentQL.Demo.Models;

[AgentQLEntity(Description = "Travel destinations available for booking")]
public class Destination
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(100)]
    public string Country { get; set; }

    [MaxLength(500)]
    public string Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PricePerNight { get; set; }

    public List<Booking> Bookings { get; set; } = [];
}
