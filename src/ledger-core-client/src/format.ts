import type { AccountType, Role } from './api'

const money = new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
export const fmtMoney = (n: number) => money.format(n)
/** Puste pole zamiast "0,00" – czytelniejsze tabele obrotów. */
export const fmtMoneyOrDash = (n: number) => (n === 0 ? '' : money.format(n))
export const fmtDate = (iso: string) => iso.slice(0, 10)

export const todayIso = () => {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
export const monthStartIso = () => todayIso().slice(0, 8) + '01'
export const yearStartIso = () => todayIso().slice(0, 5) + '01-01'

/** Akceptuje "1 234,50", "1234.5" itp. Zwraca null przy niepoprawnej wartości. */
export function parseAmount(s: string): number | null {
  const t = s.replace(/\s/g, '').replace(',', '.')
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null
  return Math.round(parseFloat(t) * 100) / 100
}

export const accountTypeLabel: Record<AccountType, string> = {
  Asset: 'Aktywa', Liability: 'Zobowiązania', Equity: 'Kapitał', Income: 'Przychody', Expense: 'Koszty',
}
export const roleLabel: Record<Role, string> = {
  Admin: 'Administrator', Accountant: 'Księgowy', Viewer: 'Podgląd',
}

import type { AccountingMode, InvoiceStatus, InvoiceType, KpirKind, VatRate } from './api'
export const vatRateLabel: Record<VatRate, string> = { R23: '23%', R8: '8%', R5: '5%', R0: '0%', Exempt: 'zw.' }
export const vatPercent: Record<VatRate, number> = { R23: 23, R8: 8, R5: 5, R0: 0, Exempt: 0 }
export const invoiceTypeLabel: Record<InvoiceType, string> = { Sales: 'Sprzedaż', Purchase: 'Zakup' }
export const statusLabel: Record<InvoiceStatus, string> = { unpaid: 'Do zapłaty', overdue: 'Po terminie', paid: 'Opłacona', canceled: 'Anulowana', correction: 'Korekta' }
export const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100
export const addDaysIso = (iso: string, days: number) => {
  const d = new Date(iso + 'T12:00:00'); d.setDate(d.getDate() + days)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export const accountingModeLabel: Record<AccountingMode, string> = { FullBooks: 'Pełna księgowość', Kpir: 'KPiR', Ryczalt: 'Ryczałt' }
export const kpirKindLabel: Record<KpirKind, string> = {
  Revenue: 'Przychód ze sprzedaży towarów i usług (kol. 7)', OtherRevenue: 'Pozostałe przychody (kol. 8)',
  GoodsPurchase: 'Zakup towarów handlowych i materiałów (kol. 10)', PurchaseCosts: 'Koszty uboczne zakupu (kol. 11)',
  OtherExpense: 'Pozostałe wydatki (kol. 13)', Research: 'Koszty B+R (kol. 13 i 16)',
}
export const salesKinds: KpirKind[] = ['Revenue', 'OtherRevenue']
export const purchaseKinds: KpirKind[] = ['GoodsPurchase', 'PurchaseCosts', 'OtherExpense', 'Research']
/** Dozwolone stawki ryczałtu ewidencjonowanego (%). */
export const ryczaltRates = [2, 3, 5.5, 8.5, 10, 12, 12.5, 14, 15, 17, 20]
