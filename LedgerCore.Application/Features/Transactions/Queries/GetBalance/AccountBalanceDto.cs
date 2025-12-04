public class AccountBalanceDto
{
    public string AccountName { get; set; }
    public string AccountCode { get; set; }

    // Ile w sumie wpadło na Winien
    public decimal TotalDebit { get; set; }

    // Ile w sumie wpadło na Ma
    public decimal TotalCredit { get; set; }

    // Wynik (Różnica)
    public decimal Balance { get; set; }
}