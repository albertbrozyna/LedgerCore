namespace LedgerCore.Domain.Enums;

/// <summary>Sposób prowadzenia ewidencji firmy: pełna księgowość (konta), podatkowa księga przychodów i rozchodów albo ewidencja ryczałtu.</summary>
public enum AccountingMode { FullBooks, Kpir, Ryczalt }

/// <summary>
/// Rodzaj zapisu faktury w KPiR (kolumny wg wzoru z rozporządzenia o prowadzeniu podatkowej księgi przychodów i rozchodów):
/// Revenue – kol. 7 (wartość sprzedanych towarów i usług), OtherRevenue – kol. 8 (pozostałe przychody),
/// GoodsPurchase – kol. 10 (zakup towarów handlowych i materiałów), PurchaseCosts – kol. 11 (koszty uboczne zakupu),
/// OtherExpense – kol. 13 (pozostałe wydatki), Research – kol. 13 oraz 16 (koszty działalności badawczo-rozwojowej).
/// </summary>
public enum KpirKind { Revenue, OtherRevenue, GoodsPurchase, PurchaseCosts, OtherExpense, Research }

public static class KpirKindExtensions
{
    public static bool IsRevenue(this KpirKind k) => k is KpirKind.Revenue or KpirKind.OtherRevenue;
}
