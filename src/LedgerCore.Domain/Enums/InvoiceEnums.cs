namespace LedgerCore.Domain.Enums;

public enum InvoiceType { Sales, Purchase }

/// <summary>Stawki VAT: 23%, 8%, 5%, 0%, zwolnione (zw).</summary>
public enum VatRate { R23, R8, R5, R0, Exempt }

public static class VatRateExtensions
{
    public static decimal Percent(this VatRate r) => r switch
    {
        VatRate.R23 => 23m, VatRate.R8 => 8m, VatRate.R5 => 5m, _ => 0m
    };
}
