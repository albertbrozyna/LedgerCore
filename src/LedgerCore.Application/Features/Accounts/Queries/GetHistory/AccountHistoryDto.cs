namespace LedgerCore.Api.Dtos;

public class AccountHistoryDto
{
    public DateTime Date { get; set; }        // Z Transakcji
    public string Description { get; set; }   // Z Transakcji
    public decimal Amount { get; set; }       // Z Wpisu
    public string Side { get; set; }          // Z Wpisu (napiszemy "Winien" lub "Ma")
}