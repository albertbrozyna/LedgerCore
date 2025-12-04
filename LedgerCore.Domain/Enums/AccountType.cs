using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerCore.Domain.Enums
{
    public enum AccountType
    {
        Asset, // Aktywa (np. Kasa, Konto bankowe, Należności)
        Liability, // Pasywa (np. Kredyty, Zobowiązania wobec dostawców)
        Equity,// Kapitał własny (Pieniądze właścicieli) 
        Income,  // Przychody (np. Sprzedaż usług)
        Expense // Koszty (np. Paliwo, Czynsz, Wynagrodzenia)
    }
} 

