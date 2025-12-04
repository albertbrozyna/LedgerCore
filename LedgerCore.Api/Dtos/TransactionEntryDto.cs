using System.ComponentModel.DataAnnotations;
using LedgerCore.Domain.Enums;

namespace LedgerCore.Api.Dtos;


public class TransactionEntryDto
{
    [Required]
    public Guid AccountId { get; set; }

    [Range(0.01, 100000000, ErrorMessage = "Kwota musi być dodatnia")]
    public decimal Amount { get; set; }

    [Required]
    public DebitCredit Side { get; set; } // 0 = Debit (Winien), 1 = Credit (Ma)
}