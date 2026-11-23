namespace LedgerCore.Application.Common.Settings;

public class AuthOptions
{
    /// <summary>Gdy true, logowanie wymaga zweryfikowanego e-maila (wymaga skonfigurowanego SMTP). Domyślnie false.</summary>
    public bool RequireEmailVerification { get; set; }
}
