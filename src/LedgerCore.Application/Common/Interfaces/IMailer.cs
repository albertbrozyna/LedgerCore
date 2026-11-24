namespace LedgerCore.Application.Common.Interfaces;

/// <summary>Szyfrowanie sekretów zapisywanych w bazie (np. hasło SMTP). Klucze są przechowywane poza bazą (folder danych).</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    /// <summary>Zwraca null, gdy nie da się odszyfrować (np. baza przeniesiona na inny komputer) – hasło trzeba wtedy wpisać ponownie.</summary>
    string? Unprotect(string protectedValue);
}

public record SmtpSettingsSnapshot(string Host, int Port, bool UseTls, string? UserName, string? Password, string FromAddress, string? FromName);
public record OutgoingMail(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>Wysyłka poczty przez SMTP firmy. Błędy zwracane jako <c>MailDeliveryException</c> z czytelnym komunikatem po polsku.</summary>
public interface IMailer
{
    Task Send(SmtpSettingsSnapshot smtp, OutgoingMail mail, CancellationToken ct);
}
