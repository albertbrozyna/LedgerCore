// 1. Główna koperta z danymi transakcji
using LedgerCore.Api.Dtos;
using System.ComponentModel.DataAnnotations;

public class CreateTransactionRequest
{
    [Required(ErrorMessage = "Opis transakcji jest wymagany")]
    public string Description { get; set; }

    public DateTime Date { get; set; }

    // Lista wpisów (musi być co najmniej 2, żeby bilans miał sens)
    [Required]
    [MinLength(2, ErrorMessage = "Transakcja musi mieć co najmniej 2 zapisy!")]
    public List<TransactionEntryDto> Entries { get; set; }
}

