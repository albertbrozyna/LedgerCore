using System;

namespace LedgerCore.Domain.Exceptions
{
    // Dziedziczymy po DomainException, jeśli istnieje w Twoim projekcie
    public class NegativeAmountException : DomainException
    {
        // Konstruktor przyjmujący kwotę, która spowodowała wyjątek
        public NegativeAmountException(decimal amount)
            : base($"Negative amount is not allowed: {amount}")
        {
        }

        // Opcjonalny konstruktor bez argumentów
        public NegativeAmountException()
            : base("Negative amount is not allowed.")
        {
        }
    }
}
