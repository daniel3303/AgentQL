using System.ComponentModel.DataAnnotations;

namespace Equibles.AgentQL.Demo.Models;

public enum LoyaltyTier
{
    [Display(Name = "Bronze")]
    Bronze,
    [Display(Name = "Silver")]
    Silver,
    [Display(Name = "Gold")]
    Gold,
    [Display(Name = "Platinum")]
    Platinum
}
