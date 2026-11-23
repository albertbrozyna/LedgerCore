import type { InvoiceItem } from './api'
import { parseAmount } from './format'

export interface BankRow { date: string; amount: number; title: string; counterparty: string }

function splitLine(line: string, delim: string): string[] {
  const out: string[] = []
  let cur = '', quoted = false
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]
    if (quoted) {
      if (ch === '"' && line[i + 1] === '"') { cur += '"'; i++ }
      else if (ch === '"') { quoted = false }
      else { cur += ch }
    } else if (ch === '"') { quoted = true }
    else if (ch === delim) { out.push(cur); cur = '' }
    else { cur += ch }
  }
  out.push(cur)
  return out.map(s => s.trim())
}

/** Obsługuje daty 2026-11-05, 05.11.2026, 05-11-2026, 05/11/2026 oraz 20261105. */
export function parseBankDate(s: string): string | null {
  const t = s.trim()
  let m = /^(\d{4})-(\d{2})-(\d{2})/.exec(t)
  if (m) return `${m[1]}-${m[2]}-${m[3]}`
  m = /^(\d{2})[.\-/](\d{2})[.\-/](\d{4})/.exec(t)
  if (m) return `${m[3]}-${m[2]}-${m[1]}`
  m = /^(\d{4})(\d{2})(\d{2})$/.exec(t)
  return m ? `${m[1]}-${m[2]}-${m[3]}` : null
}

/** Kwota ze znakiem: "-1 234,56", "1234.50", "+500,00 PLN". */
export function parseSignedAmount(s: string): number | null {
  const t = s.replace(/[\s ]/g, '').replace(/PLN|zł/gi, '')
  const neg = t.startsWith('-') || /^\(.*\)$/.test(t)
  const abs = parseAmount(t.replace(/^[-+(]/, '').replace(/\)$/, ''))
  return abs === null ? null : neg ? -abs : abs
}

export interface ParseResult { rows: BankRow[]; skipped: number }

export function parseBankCsv(text: string): ParseResult {
  const lines = text.replace(/^﻿/, '').split(/\r?\n/).filter(l => l.trim())
  if (lines.length === 0) return { rows: [], skipped: 0 }
  const delim = [';', '\t', ','].map(d => ({ d, n: lines[0].split(d).length })).sort((a, b) => b.n - a.n)[0].d
  const first = splitLine(lines[0], delim)
  const norm = first.map(h => h.toLowerCase())
  const find = (re: RegExp) => norm.findIndex(h => re.test(h))
  const hasHeader = find(/data|date/) >= 0 && find(/kwota|amount|warto/) >= 0
  const idx = hasHeader
    ? { date: find(/data|date/), amount: find(/kwota|amount|warto/), title: find(/tytu|opis|title|descr/), who: find(/kontrahent|nadawca|odbiorca|counterpart|nazwa/) }
    : { date: 0, amount: 1, title: 2, who: 3 }

  const rows: BankRow[] = []
  let skipped = 0
  for (const line of hasHeader ? lines.slice(1) : lines) {
    const c = splitLine(line, delim)
    const date = parseBankDate(c[idx.date] ?? '')
    const amount = parseSignedAmount(c[idx.amount] ?? '')
    if (!date || amount === null || amount === 0) { skipped++; continue }
    rows.push({ date, amount, title: idx.title >= 0 ? (c[idx.title] ?? '') : '', counterparty: idx.who >= 0 ? (c[idx.who] ?? '') : '' })
  }
  return { rows, skipped }
}

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
