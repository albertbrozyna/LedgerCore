namespace LedgerCore.Domain.Exceptions;

/// <summary>Nie udało się wysłać wiadomości (konfiguracja SMTP, połączenie, logowanie, adres). Komunikat jest po polsku i nie zawiera haseł.</summary>
public class MailDeliveryException(string message) : DomainException(message);
