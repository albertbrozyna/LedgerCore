using System.Net.Sockets;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Exceptions;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using MimeKit;

namespace LedgerCore.Infrastructure.Services
{
    /// <summary>Szyfrowanie sekretów (hasło SMTP) przez ASP.NET Core DataProtection; klucze leżą w folderze danych (<c>keys</c>).</summary>
    internal class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
    {
        readonly IDataProtector _protector = provider.CreateProtector("LedgerCore.Secrets.v1");
        public string Protect(string plain) => _protector.Protect(plain);
        public string? Unprotect(string protectedValue)
        {
            try { return _protector.Unprotect(protectedValue); } catch (System.Security.Cryptography.CryptographicException) { return null; }
        }
    }

    /// <summary>Wysyłka przez MailKit z tłumaczeniem błędów na czytelne komunikaty po polsku (bez haseł i szczegółów technicznych).</summary>
    internal class SmtpMailer : IMailer
    {
        public async Task Send(SmtpSettingsSnapshot s, OutgoingMail mail, CancellationToken ct)
        {
            var message = new MimeMessage();
            try { message.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(s.FromName) ? s.FromAddress : s.FromName, s.FromAddress)); }
            catch (ParseException) { throw new MailDeliveryException("Nieprawidłowy adres nadawcy w ustawieniach poczty"); }
            try { message.To.Add(MailboxAddress.Parse(mail.To)); }
            catch (ParseException) { throw new MailDeliveryException($"Nieprawidłowy adres odbiorcy: {mail.To}"); }
            message.Subject = mail.Subject;
            message.Body = new BodyBuilder { HtmlBody = mail.HtmlBody, TextBody = mail.TextBody }.ToMessageBody();

            using var client = new SmtpClient { Timeout = 15000 };
            try
            {
                var security = !s.UseTls ? SecureSocketOptions.None : s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await client.ConnectAsync(s.Host, s.Port, security, ct);
                if (!string.IsNullOrEmpty(s.UserName)) await client.AuthenticateAsync(s.UserName, s.Password ?? "", ct);
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(true, ct);
            }
            catch (AuthenticationException) { throw new MailDeliveryException("Serwer poczty odrzucił login lub hasło – sprawdź dane logowania SMTP"); }
            catch (SslHandshakeException) { throw new MailDeliveryException($"Nie udało się nawiązać bezpiecznego połączenia z {s.Host}:{s.Port} – sprawdź port i ustawienie szyfrowania (TLS)"); }
            catch (ServiceNotAuthenticatedException) { throw new MailDeliveryException("Serwer poczty wymaga logowania – podaj login i hasło SMTP"); }
            catch (SmtpCommandException e) when (e.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.SenderNotAccepted)
            { throw new MailDeliveryException(e.ErrorCode == SmtpErrorCode.RecipientNotAccepted ? $"Serwer odrzucił adres odbiorcy {mail.To}" : "Serwer odrzucił adres nadawcy – sprawdź ustawienia poczty"); }
            catch (SmtpCommandException e) { throw new MailDeliveryException($"Serwer poczty odrzucił wiadomość (kod {(int)e.StatusCode})"); }
            catch (Exception e) when (e is SocketException or IOException or TimeoutException or OperationCanceledException or SmtpProtocolException or ProtocolException)
            {
                if (e is OperationCanceledException && ct.IsCancellationRequested) throw;
                throw new MailDeliveryException($"Nie można połączyć się z serwerem poczty {s.Host}:{s.Port} – sprawdź adres, port i połączenie z internetem");
            }
        }
    }
}
