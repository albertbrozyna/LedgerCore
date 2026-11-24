import type { InvoiceItem } from './api'

/** Operacja z wyciągu w postaci potrzebnej do dopasowania (parsowanie plików robi serwer). */
export interface BankRow { date: string; amount: number; title: string; counterparty: string }

// tylko litery i cyfry: "FV/2026/1" = "FV 2026-1" (banki często zamieniają znaki specjalne w tytułach)
const squash = (s: string) => s.toLowerCase().replace(/[^\p{L}\p{N}]/gu, '')

export type Confidence = 'exact' | 'number' | 'amount' | 'none'
export interface Match { invoice: InvoiceItem | null; confidence: Confidence }

/**
 * Dopasowanie wiersza wyciągu do otwartej faktury:
 *  - wpływ (kwota > 0) → faktura sprzedaży, wypłata (< 0) → faktura zakupu,
 *  - numer faktury w tytule (z pominięciem spacji) + dokładna kwota = "exact",
 *  - sam numer w tytule = "number" (np. płatność częściowa),
 *  - dokładna kwota i nazwa kontrahenta w opisie operacji = "amount".
 */
export function matchRow(row: BankRow, open: InvoiceItem[]): Match {
  const type = row.amount > 0 ? 'Sales' : 'Purchase'
  const abs = Math.abs(row.amount)
  const pool = open.filter(i => i.type === type && i.remaining > 0)
  const hay = squash(row.title)
  const byNumber = pool.filter(i => hay.includes(squash(i.number)))
  const exact = byNumber.find(i => i.remaining === abs)
  if (exact) return { invoice: exact, confidence: 'exact' }
  if (byNumber.length === 1) return { invoice: byNumber[0], confidence: 'number' }
  const who = squash(row.counterparty + row.title)
  const byAmount = pool.filter(i => i.remaining === abs && who.includes(squash(i.contractorName).slice(0, 12)))
  if (byAmount.length === 1) return { invoice: byAmount[0], confidence: 'amount' }
  return { invoice: null, confidence: 'none' }
}
