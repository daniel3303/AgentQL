using System.ComponentModel.DataAnnotations;

namespace Equibles.AgentQL.Demo.Models;

public enum BookingStatus
{
    [Display(Name = "Pending")]
    Pending,
    [Display(Name = "Confirmed")]
    Confirmed,
    [Display(Name = "Cancelled")]
    Cancelled,
    [Display(Name = "Completed")]
    Completed
}
