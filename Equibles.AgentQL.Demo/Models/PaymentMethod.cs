using System.ComponentModel.DataAnnotations;

namespace Equibles.AgentQL.Demo.Models;

public enum PaymentMethod
{
    [Display(Name = "Credit Card")]
    CreditCard,
    [Display(Name = "Debit Card")]
    DebitCard,
    [Display(Name = "Bank Transfer")]
    BankTransfer,
    [Display(Name = "Cash")]
    Cash
}
